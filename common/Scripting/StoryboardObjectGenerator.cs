namespace StorybrewCommon.Scripting;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Animations;
using BrewLib.Graphics.Compression;
using BrewLib.Util;
using Mapset;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Storyboarding;
using Subtitles;
using Subtitles.Parsers;
using Util;

///<summary> Defines a storyboard script to be generated. </summary>
public abstract class StoryboardObjectGenerator : Script
{
    static readonly AsyncLocal<StoryboardObjectGenerator> instance = new();

    readonly ConfigurableField[] configurableFields;
    readonly Lazy<ImageCompressor> imageCompressor;
    GeneratorContext context;

    ///<summary>Reserved</summary>
    protected StoryboardObjectGenerator()
    {
        imageCompressor = new(() =>
        {
            IntegratedCompressor compressor = new();
            disposables.Add(compressor);
            return compressor;
        });

        configurableFields = GetType().GetFields().Select(field =>
        {
            var configurable = field.GetCustomAttribute<ConfigurableAttribute>(true);
            return configurable is null ? null : new { Field = field, Configurable = configurable };
        }).Where(item => item is not null).Select((item, order) => new ConfigurableField(item.Field, item.Configurable,
            item.Field.GetValue(this), item.Field.GetCustomAttribute<GroupAttribute>(true)?.Name?.Trim(),
            item.Field.GetCustomAttribute<DescriptionAttribute>(true)?.Content?.Trim(), order)).ToArray();
    }

    ///<summary> Gets the currently executing script. </summary>
    public static StoryboardObjectGenerator Current => instance.Value;

    /// <summary>
    ///     Set to <see langword="true"/> if this script uses multiple threads. It prevents other effects from updating
    ///     in parallel to this one.
    /// </summary>
    protected bool Multithreaded { get; set; }

    ///<summary> Gets the texture and image compressor for this script. </summary>
    public ImageCompressor Compressor => imageCompressor.Value;

    ///<summary> Gets the currently selected beatmap. </summary>
    public Beatmap Beatmap => context.Beatmap;

    ///<summary> Path to the directory of this project. </summary>
    public string ProjectPath => context.ProjectPath;

    ///<summary> Path to the asset library directory of this project. </summary>
    public string AssetPath => context.ProjectAssetPath;

    ///<summary> Path to the mapset of this project. </summary>
    public string MapsetPath => context.MapsetPath;

    /// <summary> Creates or retrieves a layer. </summary>
    /// <remarks> The identifier will be shown in the editor as <b>Effect name (<paramref name="name"/>)</b>. </remarks>
    public StoryboardLayer GetLayer(string name) => context.GetLayer(name);

    ///<summary> Gets the beatmap with the specified difficulty name, or if not found, the default beatmap. </summary>
    public Beatmap GetBeatmap(string name) => context.Beatmaps.FirstOrDefault(b => b.Name == name);

    /// <summary> Watches a dependency at <paramref name="path"/>. </summary>
    public void AddDependency(string path) => context.AddDependency(path);

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
    public void Generate(GeneratorContext context)
    {
        if (instance.Value is not null) throw new InvalidOperationException("A script is already running in this thread");
        try
        {
            this.context = context;
            rnd = new(RandomSeed);
            instance.Value = this;

            Generate();
            context.Multithreaded = Multithreaded;
        }
        finally
        {
            instance.Value = null;
            this.context = null;

            foreach (var disposable in disposables) disposable.Dispose();
        }
    }

    ///<summary> Main body for storyboard generation. </summary>
    protected abstract void Generate();

    #region File loading

    internal readonly Dictionary<string, Image<Rgba32>> bitmaps = [];
    readonly List<IDisposable> disposables = [];

    /// <summary> Returns a <see cref="Image"/> from the project's directory. </summary>
    /// <param name="path"> The image path, relative to the project's folder. </param>
    /// <param name="watch"> Watch the file as a dependency. </param>
    public Image<Rgba32> GetProjectBitmap(string path, bool watch = true)
        => getBitmap(Path.Combine(context.ProjectPath, path), null, watch);

    /// <summary> Returns a <see cref="Image"/> from the mapset's directory. </summary>
    /// <param name="path"> The image path, relative to the mapset's folder. </param>
    /// <param name="watch"> Watch the file as a dependency. </param>
    public Image<Rgba32> GetMapsetBitmap(string path, bool watch = true) => getBitmap(Path.Combine(context.MapsetPath, path),
        Path.Combine(context.ProjectAssetPath, path), watch);

    Image<Rgba32> getBitmap(string path, string alternatePath, bool watch)
    {
        path = Path.GetFullPath(path);
        if (bitmaps.TryGetValue(path, out var bitmap)) return bitmap;

        using var stream = File.OpenRead(path);
        if (alternatePath is not null && !File.Exists(path))
        {
            alternatePath = Path.GetFullPath(alternatePath);
            if (watch) context.AddDependency(alternatePath);

            disposables.Add(bitmaps[path] = bitmap = Image.Load<Rgba32>(stream));
        }
        else
        {
            if (watch) context.AddDependency(path);
            disposables.Add(bitmaps[path] = bitmap = Image.Load<Rgba32>(stream));
        }

        return bitmap;
    }

    /// <summary> Opens a file, relative to the project folder, in read-only mode. </summary>
    /// <remarks> Dispose of the returned <see cref="Stream"/> as soon as possible. </remarks>
    public Stream OpenProjectFile(string path, bool watch = true) => openFile(Path.Combine(context.ProjectPath, path), watch);

    /// <summary> Opens a file, relative to the mapset folder, in read-only mode. </summary>
    /// <remarks> Dispose of the returned <see cref="Stream"/> as soon as possible. </remarks>
    public Stream OpenMapsetFile(string path, bool watch = true) => openFile(Path.Combine(context.MapsetPath, path), watch);

    FileStream openFile(string path, bool watch)
    {
        path = Path.GetFullPath(path);
        if (watch) context.AddDependency(path);

        var stream = File.OpenRead(path);
        disposables.Add(stream);
        return stream;
    }

    #endregion

    #region Random

    /// <summary/>
    [Group("Common"), Description("Changes the result of Random(...) calls."), Configurable]
    public int RandomSeed;

    FastRandom rnd;

    /// <summary> Gets a random integer between <paramref name="minValue"/> and <paramref name="maxValue"/>. </summary>
    public int Random(int minValue, int maxValue) => rnd.Next(minValue, maxValue);

    /// <summary> Gets a random integer between 0 and <paramref name="maxValue"/>. </summary>
    public int Random(int maxValue) => rnd.Next(maxValue);

    /// <summary>
    ///     Gets a random double-precision floating-point number between <paramref name="minValue"/> and
    ///     <paramref name="maxValue"/>.
    /// </summary>
    public double Random(double minValue, double maxValue) => minValue + (maxValue - minValue) * rnd.NextDouble();

    /// <summary> Gets a random double-precision floating-point number between 0 and <paramref name="maxValue"/>. </summary>
    public double Random(double maxValue) => rnd.NextDouble() * maxValue;

    /// <summary>
    ///     Gets a random single-precision floating-point number between <paramref name="minValue"/> and
    ///     <paramref name="maxValue"/>.
    /// </summary>
    public float Random(float minValue, float maxValue) => (float)(minValue + (maxValue - minValue) * rnd.NextDouble());

    /// <summary> Gets a random single-precision floating-point number between 0 and <paramref name="maxValue"/>. </summary>
    public float Random(float maxValue) => (float)(rnd.NextDouble() * maxValue);

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
        return fft.GetSpan();
    }

    /// <summary> Gets the Fast Fourier Transform of the song at <paramref name="time"/>, with the given amount of magnitudes. </summary>
    public Span<float> GetFft(float time,
        int magnitudes,
        string path = null,
        OsbEasing easing = OsbEasing.None,
        float frequencyCutOff = 0)
    {
        var fft = GetFft(time, path);
        if (magnitudes == fft.Length && easing is OsbEasing.None) return fft;

        var usedFftLength = frequencyCutOff > 0 ?
            (int)(frequencyCutOff / (context.GetFftFrequency(path) * .5f) * fft.Length) :
            fft.Length;

        UnmanagedBuffer<float> resultFft = new(magnitudes);
        disposables.Add(resultFft);

        var resultSpan = resultFft.GetSpan();

        var baseIndex = 0;
        for (var i = 0; i < magnitudes; ++i)
        {
            var progress = easing.Ease((float)i / magnitudes);
            var index = Math.Min((int)Math.Max(baseIndex + 1, progress * usedFftLength), usedFftLength - 1);

            var value = 0f;
            for (var v = baseIndex; v < index; ++v) value = Math.Max(value, fft[index]);

            resultSpan[i] = value;
            baseIndex = index;
        }

        return resultSpan;
    }

    #endregion

    #region Subtitles

    static readonly SrtParser srt = new();
    static readonly AssParser ass = new();
    static readonly SbvParser sbv = new();
    internal readonly Dictionary<string, FontGenerator> fonts = [];

    ///<summary> Loads subtitles from a given subtitle file. </summary>
    public SubtitleSet LoadSubtitles(string path)
    {
        context.AddDependency(Path.Combine(context.ProjectPath, path));
        return Path.GetExtension(path) switch
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
    public FontGenerator LoadFont(string directory, FontDescription description, params FontEffect[] effects)
        => LoadFont(directory, false, description, effects);

    /// <summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
    /// <param name="directory"> The relative path to place the font textures. </param>
    /// <param name="asAsset"> Output textures in the asset library directory. </param>
    /// <param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
    /// <param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
    /// <exception cref="InvalidOperationException"/>
    public FontGenerator LoadFont(string directory, bool asAsset, FontDescription description, params FontEffect[] effects)
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
        return fontGenerator;
    }

    #endregion

    #region Configuration

    /// <summary/>
    public void UpdateConfiguration(EffectConfig config)
    {
        if (context is not null) throw new InvalidOperationException();

        var remainingFieldNames = config.FieldNames.ToList();
        foreach (var (field, configurableAttribute, o, beginsGroup, description, order) in configurableFields)
        {
            NamedValue[] allowedValues = null;

            var fieldType = field.FieldType;
            if (fieldType.IsEnum)
            {
                var enumValues = Enum.GetValues(fieldType);
                fieldType = Enum.GetUnderlyingType(fieldType);

                allowedValues = new NamedValue[enumValues.Length];
                for (var i = 0; i < enumValues.Length; ++i)
                {
                    var value = enumValues.GetValue(i);
                    allowedValues[i] = new(value.ToString(), Convert.ChangeType(value, fieldType, CultureInfo.InvariantCulture));
                }
            }

            try
            {
                var displayName = configurableAttribute.DisplayName;
                var initialValue = Convert.ChangeType(o, fieldType, CultureInfo.InvariantCulture);
                config.UpdateField(field.Name, displayName, description, order, fieldType, initialValue, allowedValues,
                    beginsGroup);

                var value = config.GetValue(field.Name);
                field.SetValue(this, value);

                remainingFieldNames.Remove(field.Name);
            }
            catch (Exception e)
            {
                Trace.TraceError($"Updating configuration for {field.Name} with type {fieldType}:\n{e}");
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
                Trace.TraceError($"Applying configuration for {field.Name}:\n{e}");
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