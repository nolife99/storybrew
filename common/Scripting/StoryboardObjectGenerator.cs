namespace StorybrewCommon.Scripting;

using System;
using System.Globalization;
using System.IO;
using System.IO.Hashing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Animations;
using BrewLib.Memory;
using BrewLib.Util;
using Mapset;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Storyboarding;
using Subtitles;
using Subtitles.Parsers;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary;
using Util;
using ZLinq;
using Image = SixLabors.ImageSharp.Image;

///<summary> Defines a storyboard script to be generated. </summary>
public abstract class StoryboardObjectGenerator : Script
{
    static readonly AsyncLocal<StoryboardObjectGenerator> instance = new();

    readonly ConfigurableField[] configurableFields;
    GeneratorContext context;

    /// <summary> Reserved </summary>
    protected StoryboardObjectGenerator()
        => configurableFields = GetType()
            .GetFields()
            .AsValueEnumerable()
            .Select(field => (field, field.GetCustomAttribute<ConfigurableAttribute>(true)))
            .Where(item => item.Item2 is not null)
            .Select((item, order) => new ConfigurableField(item.field,
                item.Item2,
                item.field.GetValue(this),
                item.field.GetCustomAttribute<GroupAttribute>(true)?.Name?.Trim(),
                item.field.GetCustomAttribute<DescriptionAttribute>(true)?.Content?.Trim(),
                order))
            .ToArray();

    ///<summary> Gets the currently executing script. </summary>
    public static StoryboardObjectGenerator Current => instance.Value;

    /// <summary>
    /// Set to <see langword="true"/> if this script uses multiple threads. It prevents other effects from updating in
    /// parallel to this one.
    /// </summary>
    protected bool Multithreaded { get; set; }

    ///<summary> Gets the currently selected beatmap. </summary>
    public Beatmap Beatmap => context.Beatmap;

    ///<summary> Path to the directory of this project. </summary>
    public string ProjectPath => context.ProjectPath;

    ///<summary> Path to the asset library directory of this project. </summary>
    public string AssetPath => context.ProjectAssetPath;

    ///<summary> Path to the mapset of this project. </summary>
    public string MapsetPath => context.MapsetPath;

    /// <summary> Creates or retrieves a layer. </summary>
    /// <remarks> The identifier will be shown in the editor as <b> Effect name (<paramref name="name"/>) </b>. </remarks>
    public StoryboardLayer GetLayer(string name) => context.GetLayer(name);

    ///<summary> Gets the beatmap with the specified difficulty name, or if not found, the default beatmap. </summary>
    public Beatmap GetBeatmap(string name)
    {
        foreach (var beatmap in context.Beatmaps)
            if (beatmap.Name == name)
                return beatmap;

        return null;
    }

    /// <summary> Watches a dependency at <paramref name="path"/>. </summary>
    public void AddDependency(string path) => context.AddDependency(path);

    /// <summary> Logs a message on the effect. </summary>
    /// <param name="message"> Message to be displayed. </param>
    [OverloadResolutionPriority(1)]
    public void Log(scoped ref PoolingInterpolatedStringHandler message)
    {
        using (message) context.AppendLog(message.Result);
    }

    /// <summary> Logs a message on the effect. </summary>
    /// <param name="message"> Message to be displayed. </param>
    public void Log(object message) => context.AppendLog(message.ToString());

    /// <summary> Throws an exception if <paramref name="condition"/> returns false. </summary>
    /// <param name="condition"> The condition to be asserted. </param>
    /// <param name="message"> The message to display if assertion fails. </param>
    /// <param name="line"> The line at which the condition should be taken into account. </param>
    public static void Assert(bool condition, string message = null, [CallerLineNumber] int line = -1)
    {
        if (!condition)
            throw new ArgumentException(message is not null ?
                $"Assertion failed line {line}: {message}" :
                $"Assertion failed line {line}");
    }

    ///<summary> Generates the storyboard created by this script. </summary>
    public Exception Generate(GeneratorContext context,
        Action<Action, CancellationToken> scriptWrapper,
        CancellationToken token)
    {
        this.context = context;
        rnd = Crc64.HashToUInt64(MemoryMarshal.AsBytes<int>(new(ref RandomSeed)));
        instance.Value = this;

        try
        {
            scriptWrapper(Generate, token);
        }
        catch (Exception e)
        {
            return e;
        }
        finally
        {
            context.Multithreaded = Multithreaded;

            instance.Value = null;
            this.context = null;

            bitmaps.Dispose();
            fonts.Dispose();

            foreach (var disposable in disposables) disposable.Dispose();
            disposables.Dispose();
        }

        return null;
    }

    ///<summary> Main body for storyboard generation. </summary>
    protected abstract void Generate();

    #region File loading

    internal readonly PooledDictionary<string, Image<Rgba32>> bitmaps = new();
    internal readonly PooledList<IDisposable> disposables = new();

    /// <summary> Returns a <see cref="SixLabors.ImageSharp.Image"/> from the project's directory. </summary>
    /// <param name="path"> The image path, relative to the project's folder. </param>
    /// <param name="watch"> Watch the file as a dependency. </param>
    public Image<Rgba32> GetProjectBitmap(string path, bool watch = true)
        => getBitmap(Path.Combine(context.ProjectPath, path), null, watch);

    /// <summary> Returns a <see cref="SixLabors.ImageSharp.Image"/> from the mapset's directory. </summary>
    /// <param name="path"> The image path, relative to the mapset's folder. </param>
    /// <param name="watch"> Watch the file as a dependency. </param>
    public Image<Rgba32> GetMapsetBitmap(string path, bool watch = true)
        => getBitmap(Path.Combine(context.MapsetPath, path), Path.Combine(context.ProjectAssetPath, path), watch);

    Image<Rgba32> getBitmap(string path, string alternatePath, bool watch)
    {
        path = Path.GetFullPath(path);
        if (bitmaps.TryGetValue(path, out var bitmap)) return bitmap;

        if (alternatePath is not null && !File.Exists(path))
        {
            alternatePath = Path.GetFullPath(alternatePath);
            if (watch) context.AddDependency(alternatePath);

            disposables.Add(bitmaps[path] = bitmap = Image.Load<Rgba32>(alternatePath));
        }
        else
        {
            if (watch) context.AddDependency(path);
            disposables.Add(bitmaps[path] = bitmap = Image.Load<Rgba32>(path));
        }

        return bitmap;
    }

    /// <summary> Opens a file, relative to the project folder, in read-only mode. </summary>
    /// <remarks> Dispose of the returned <see cref="Stream"/> as soon as possible. </remarks>
    public Stream OpenProjectFile(string path, bool watch = true)
        => openFile(Path.Combine(context.ProjectPath, path), watch);

    /// <summary> Opens a file, relative to the mapset folder, in read-only mode. </summary>
    /// <remarks> Dispose of the returned <see cref="Stream"/> as soon as possible. </remarks>
    public Stream OpenMapsetFile(string path, bool watch = true)
        => openFile(Path.Combine(context.MapsetPath, path), watch);

    FileStream openFile(string path, bool watch)
    {
        path = Path.GetFullPath(path);
        if (watch) context.AddDependency(path);

        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        disposables.Add(stream);
        return stream;
    }

    #endregion

    #region Random

    /// <summary/>
    [Group("Common"), Description("Changes the result of Random(...) calls."), Configurable]
    public int RandomSeed;

    ulong rnd;

    /// <summary> Gets a random integer between <paramref name="minValue"/> and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Random(int minValue, int maxValue) => minValue + SDL.RandR(ref rnd, maxValue - minValue);

    /// <summary> Gets a random integer less than <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Random(int maxValue) => SDL.RandR(ref rnd, maxValue);

    /// <summary>
    /// Gets a random double-precision floating-point number between <paramref name="minValue"/> and
    /// <paramref name="maxValue"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Random(double minValue, double maxValue) => minValue + (maxValue - minValue) * SDL.RandFR(ref rnd);

    /// <summary> Gets a random double-precision floating-point number between 0 and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Random(double maxValue) => SDL.RandFR(ref rnd) * maxValue;

    /// <summary>
    /// Gets a random single-precision floating-point number between <paramref name="minValue"/> and
    /// <paramref name="maxValue"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Random(float minValue, float maxValue) => minValue + (maxValue - minValue) * SDL.RandFR(ref rnd);

    /// <summary> Gets a random single-precision floating-point number between 0 and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Random(float maxValue) => SDL.RandFR(ref rnd) * maxValue;

    #endregion

    #region Audio

    ///<summary> Gets the audio duration of the beatmap in milliseconds. </summary>
    public float AudioDuration => context.AudioDuration;

    /// <summary> Gets the Fast Fourier Transform of the song at <paramref name="time"/>, with default magnitudes. </summary>
    public Span<float> GetFft(float time, string path = null, bool splitChannels = false)
    {
        if (path is not null) AddDependency(path);
        var fft = context.GetFft(time, path, splitChannels);
        disposables.Add(fft);
        return fft.Memory.Span;
    }

    /// <summary> Gets the Fast Fourier Transform of the song at <paramref name="time"/>, with the given amount of magnitudes. </summary>
    public Span<float> GetFft(float time,
        int magnitudes,
        string path = null,
        OsbEasing easing = OsbEasing.None,
        float frequencyCutOff = 0)
    {
        if (path is not null) AddDependency(path);

        var fft = context.GetFft(time, path);
        var fftSpan = fft.Memory.Span;

        if (magnitudes == fftSpan.Length && easing is OsbEasing.None)
        {
            disposables.Add(fft);
            return fftSpan;
        }

        var usedFftLength = frequencyCutOff > 0 ?
            float.ConvertToIntegerNative<int>(frequencyCutOff / (context.GetFftFrequency(path) * .5f) *
                fftSpan.Length) :
            fftSpan.Length;

        var resultFft = MemoryAllocator.Default.Allocate<float>(magnitudes);
        disposables.Add(resultFft);

        var resultSpan = resultFft.Memory.Span;

        var baseIndex = 0;
        for (var i = 0; i < resultSpan.Length; ++i)
        {
            var progress = easing.Ease((float)i / magnitudes);
            var index = int.Min((int)float.Max(baseIndex + 1, progress * usedFftLength), usedFftLength - 1);

            resultSpan[i] = fftSpan[index];
            baseIndex = index;
        }

        fft.Dispose();
        return resultSpan;
    }

    #endregion

    #region Subtitles

    static readonly SrtParser srt = new();
    static readonly AssParser ass = new();
    static readonly SbvParser sbv = new();
    readonly PooledDictionary<string, FontGenerator> fonts = new();

    ///<summary> Loads subtitles from a given subtitle file. </summary>
    public SubtitleSet LoadSubtitles(string path)
    {
        context.AddDependency(Path.Combine(context.ProjectPath, path));
        return Path.GetExtension(path.AsSpan()) switch
        {
            ".srt" => srt.Parse(path),
            ".ssa" or ".ass" => ass.Parse(path),
            ".sbv" => sbv.Parse(path),
            _ => throw new NotSupportedException($"{Path.GetExtension(path)} isn't a supported subtitle format")
        };
    }

    /// <summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
    /// <param name="directory"> The path to the font file. </param>
    /// <param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
    /// <param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
    public FontGenerator LoadFont(string directory,
        FontDescription description,
        params ReadOnlySpan<FontEffect> effects)
        => LoadFont(directory, false, description, effects);

    /// <summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
    /// <param name="directory"> The relative path to place the font textures. </param>
    /// <param name="asAsset"> Output textures in the asset library directory. </param>
    /// <param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
    /// <param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
    /// <exception cref="InvalidOperationException"/>
    public FontGenerator LoadFont(string directory,
        bool asAsset,
        FontDescription description,
        params ReadOnlySpan<FontEffect> effects)
    {
        var assetDirectory = asAsset ? context.ProjectAssetPath : context.MapsetPath;
        var fontDirectory = Path.GetFullPath(Path.Combine(assetDirectory, directory));

        if (fonts.ContainsKey(fontDirectory))
            throw new InvalidOperationException($"This effect already generated a font inside \"{fontDirectory}\"");

        if (Directory.Exists(fontDirectory))
            foreach (var file in Directory.EnumerateFiles(fontDirectory, "*.png"))
                PathHelper.SafeDelete(file);
        else Directory.CreateDirectory(fontDirectory);

        FontGenerator fontGenerator = new(directory, description, effects, context.ProjectPath, assetDirectory);
        fonts[fontDirectory] = fontGenerator;
        disposables.Add(fontGenerator);
        return fontGenerator;
    }

    #endregion

    #region Configuration

    /// <summary> Updates the configuration fields for the storyboard object generator using the provided effect configuration. </summary>
    public void UpdateConfiguration(EffectConfig config)
    {
        if (context is not null) throw new InvalidOperationException();

        using var remainingFieldNames = TempList.Create(config.FieldNames);
        foreach (var (field, configurableAttribute, o, beginsGroup, description, order) in configurableFields)
        {
            NamedValue[] allowedValues = null;

            var fieldType = field.FieldType;
            if (fieldType.IsEnum)
            {
                var enumValues = fieldType.GetEnumValues();
                fieldType = fieldType.GetEnumUnderlyingType();

                allowedValues = new NamedValue[enumValues.Length];
                for (var i = 0; i < enumValues.Length; ++i)
                {
                    var value = enumValues.GetValue(i);
                    allowedValues[i] = new(value.ToString(),
                        Convert.ChangeType(value, fieldType, CultureInfo.InvariantCulture));
                }
            }

            try
            {
                var displayName = configurableAttribute.DisplayName;
                var initialValue = Convert.ChangeType(o, fieldType, CultureInfo.InvariantCulture);
                config.UpdateField(field.Name,
                    displayName,
                    description,
                    order,
                    fieldType,
                    initialValue,
                    allowedValues,
                    beginsGroup);

                var value = config.GetValue(field.Name);
                field.SetValue(this, value);

                remainingFieldNames.Remove(field.Name);
            }
            catch (Exception e)
            {
                SDL.LogError(LogCategory.Test, $"Updating configuration for {field.Name} with type {fieldType}:\n{e}");
            }
        }

        foreach (var remaining in remainingFieldNames) config.RemoveField(remaining);
    }

    /// <summary/>
    public void ApplyConfiguration(EffectConfig config)
    {
        if (context is not null) throw new InvalidOperationException();

        foreach (var configurableField in configurableFields)
        {
            var field = configurableField.Field;
            try
            {
                var value = config.GetValue(field.Name);
                field.SetValue(this, value);
            }
            catch (Exception e)
            {
                SDL.LogError(LogCategory.Test, $"Applying configuration for {field.Name}:\n{e}");
            }
        }
    }

    record struct ConfigurableField(FieldInfo Field,
        ConfigurableAttribute Attribute,
        object InitialValue,
        string BeginsGroup,
        string Description,
        int Order);

    #endregion
}