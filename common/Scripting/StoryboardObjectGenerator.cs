using BrewLib.Util;
using BrewLib.Util.Compression;
using StorybrewCommon.Animations;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;
using StorybrewCommon.Subtitles;
using StorybrewCommon.Subtitles.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Tiny;
using System.IO.Compression;
using System.Globalization;
using System.Threading;
using System.Runtime.InteropServices;

namespace StorybrewCommon.Scripting;

///<summary> Defines a storyboard script to run and generate. </summary>
public abstract class StoryboardObjectGenerator : Script
{
    static readonly AsyncLocal<StoryboardObjectGenerator> instance = new();

    ///<summary> Gets the currently executing script. </summary>
    public static StoryboardObjectGenerator Current => instance.Value;

    readonly List<ConfigurableField> configurableFields;
    GeneratorContext context;

    ///<summary> Set to <see langword="true"/> if this script uses multiple threads. It prevents other effects from updating in parallel to this one. </summary>
    public bool Multithreaded { get; protected set; }
    
    ///<summary> Gets the texture and image compressor for this script. </summary>
    public ImageCompressor Compressor { get; private set; }

    ///<summary> Creates or retrieves a layer. </summary>
    ///<remarks> The identifier will be shown in the editor as <b>Effect name (<paramref name="name"/>)</b>. </remarks>
    public StoryboardLayer GetLayer(string name) => context.GetLayer(name);

    ///<summary> Gets the currently selected beatmap. </summary>
    public Beatmap Beatmap => context.Beatmap;

    ///<summary> Gets the beatmap with the specified difficulty name, or if not found, the default beatmap. </summary>
    public Beatmap GetBeatmap(string name) => context.Beatmaps.FirstOrDefault(b => b.Name == name);

    ///<summary> Path to the directory of this project. </summary>
    public string ProjectPath => context.ProjectPath;

    ///<summary> Path to the asset library directory of this project. </summary>
    public string AssetPath => context.ProjectAssetPath;

    ///<summary> Path to the mapset of this project. </summary>
    public string MapsetPath => context.MapsetPath;

    ///<summary>Reserved</summary>
    protected StoryboardObjectGenerator()
    {
        var fields = GetType().GetFields();
        configurableFields = new(fields.Length);

        for (int i = 0, order = 0; i < fields.Length; ++i)
        {
            var field = fields[i];
            var configurable = field.GetCustomAttribute<ConfigurableAttribute>(true);
            if (!field.FieldType.IsEnum && !ObjectSerializer.Supports(field.FieldType.FullName) || configurable is null) continue;

            configurableFields.Add(new(field, configurable, field.GetValue(this),
                field.GetCustomAttribute<GroupAttribute>(true)?.Name?.Trim(), field.GetCustomAttribute<DescriptionAttribute>(true)?.Content?.Trim(), order++));
        }
    }

    ///<summary> Watches a dependency at <paramref name="path"/>. </summary>
    public void AddDependency(string path) => context.AddDependency(path);

    ///<summary> Logs a message on the effect. </summary>
    ///<param name="message"> Message to be displayed. </param>
    public void Log(object message) => context.AppendLog(message.ToString());

    ///<summary> Throws an exception if <paramref name="condition"/> returns false. </summary>
    ///<param name="condition"> The condition to be asserted. </param>
    ///<param name="message"> The message to display if assertion fails. </param>
    ///<param name="line"> The line at which the condition should be taken into account. </param>
    public static void Assert(bool condition, string message = null, [CallerLineNumber] int line = -1)
    {
        if (!condition) throw new ArgumentException(message is not null ? $"Assertion failed line {line}: {message}" : $"Assertion failed line {line}");
    }

    #region File loading

    readonly Dictionary<string, Bitmap> bitmaps = [];

    ///<summary> Returns a <see cref="Bitmap"/> from the project's directory. </summary>
    ///<param name="path"> The image path, relative to the project's folder. </param>
    ///<param name="watch"> Watch the file as a dependency. </param>
    public Bitmap GetProjectBitmap(string path, bool watch = true) => getBitmap(Path.Combine(context.ProjectPath, path), null, watch);

    ///<summary> Returns a <see cref="Bitmap"/> from the mapset's directory. </summary>
    ///<param name="path"> The image path, relative to the mapset's folder. </param>
    ///<param name="watch"> Watch the file as a dependency. </param>
    public Bitmap GetMapsetBitmap(string path, bool watch = true) => getBitmap(Path.Combine(context.MapsetPath, path), Path.Combine(context.ProjectAssetPath, path), watch);

    Bitmap getBitmap(string path, string alternatePath, bool watch)
    {
        path = Path.GetFullPath(path);
        if (!bitmaps.TryGetValue(path, out var bitmap)) using (var stream = File.OpenRead(path))
        {
            if (alternatePath is not null && !File.Exists(path))
            {
                alternatePath = Path.GetFullPath(alternatePath);
                if (watch) context.AddDependency(alternatePath);

                try
                {
                    bitmaps[path] = bitmap = Misc.WithRetries(() => new Bitmap(stream));
                }
                catch (FileNotFoundException e)
                {
                    throw new FileNotFoundException(path, e);
                }
            }
            else
            {
                if (watch) context.AddDependency(path);
                bitmaps[path] = bitmap = Misc.WithRetries(() => new Bitmap(stream));
            }
        }
        return bitmap;
    }

    ///<summary> Opens a file, relative to the project folder, in read-only mode. </summary>
    ///<remarks> Dispose of the returned <see cref="Stream"/> as soon as possible. </remarks>
    public Stream OpenProjectFile(string path, bool watch = true) => openFile(Path.Combine(context.ProjectPath, path), watch);

    ///<summary> Opens a file, relative to the mapset folder, in read-only mode. </summary>
    ///<remarks> Dispose of the returned <see cref="Stream"/> as soon as possible. </remarks>
    public Stream OpenMapsetFile(string path, bool watch = true) => openFile(Path.Combine(context.MapsetPath, path), watch);

    FileStream openFile(string path, bool watch)
    {
        path = Path.GetFullPath(path);
        if (watch) context.AddDependency(path);
        return Misc.WithRetries(() => File.OpenRead(path));
    }

    #endregion

    #region Random

    ///<summary/>
    [Group("Common")][Description("Changes the result of Random(...) calls.")]
    [Configurable] public int RandomSeed;

    FastRandom rnd;

    ///<summary> Gets a random integer between <paramref name="minValue"/> and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Random(int minValue, int maxValue) => rnd.Next(minValue, maxValue);

    ///<summary> Gets a random integer between 0 and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Random(int maxValue) => rnd.Next(maxValue);

    ///<summary> Gets a random double-precision floating-point number between <paramref name="minValue"/> and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Random(double minValue, double maxValue)
    {
        if (minValue == maxValue) return minValue;
        return minValue + (maxValue - minValue) * rnd.NextDouble();
    }

    ///<summary> Gets a random double-precision floating-point number between 0 and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Random(double maxValue)
    {
        if (maxValue == 0) return 0;
        return rnd.NextDouble() * maxValue;
    }

    ///<summary> Gets a random single-precision floating-point number between <paramref name="minValue"/> and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Random(float minValue, float maxValue) => (float)Random((double)minValue, maxValue);

    ///<summary> Gets a random single-precision floating-point number between 0 and <paramref name="maxValue"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Random(float maxValue) => (float)Random((double)maxValue);

    #endregion

    #region Audio

    ///<summary> Gets the audio duration of the beatmap in milliseconds. </summary>
    public double AudioDuration => context.AudioDuration;

    ///<summary> Gets the Fast Fourier Transform of the song at <paramref name="time"/>, with default magnitudes. </summary>
    public float[] GetFft(double time, string path = null, bool splitChannels = false)
    {
        if (path is not null) AddDependency(path);
        return context.GetFft(time, path, splitChannels);
    }

    ///<summary> Gets the Fast Fourier Transform of the song at <paramref name="time"/>, with the given amount of magnitudes. </summary>
    public float[] GetFft(double time, int magnitudes, string path = null, OsbEasing easing = OsbEasing.None, float frequencyCutOff = 0)
    {
        var fft = GetFft(time, path);
        if (magnitudes == fft.Length && easing is OsbEasing.None) return fft;

        var usedFftLength = frequencyCutOff > 0 ? (int)(frequencyCutOff / (context.GetFftFrequency(path) * .5) * fft.Length) : fft.Length;
        var resultFft = new float[magnitudes];

        var baseIndex = 0;
        for (var i = 0; i < magnitudes; ++i)
        {
            var progress = EasingFunctions.Ease(easing, (double)i / magnitudes);
            var index = Math.Min((int)Math.Max(baseIndex + 1, progress * usedFftLength), usedFftLength - 1);

            var value = 0f;
            for (var v = baseIndex; v < index; ++v) value = Math.Max(value, fft[index]);

            resultFft[i] = value;
            baseIndex = index;
        }
        return resultFft;
    }

    #endregion

    #region Subtitles

    static readonly SubtitleParser srt = new SrtParser(), ass = new AssParser(), sbv = new SbvParser();

    internal readonly Dictionary<string, FontGenerator> fonts = [];
    string fontCacheDirectory => Path.Combine(context.ProjectPath, ".cache");

    ///<summary> Loads subtitles from a given subtitle file. </summary>
    public SubtitleSet LoadSubtitles(string path)
    {
        context.AddDependency(Path.Combine(context.ProjectPath, path));
        return Path.GetExtension(path) switch
        {
            ".srt" => srt.Parse(path),
            ".ssa" or ".ass" => ass.Parse(path),
            ".sbv" => sbv.Parse(path),
            _ => throw new NotSupportedException($"{Path.GetExtension(path)} isn't a supported subtitle format"),
        };
    }

    ///<summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
    ///<param name="directory"> The path to the font file. </param>
    ///<param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
    ///<param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
    public FontGenerator LoadFont(string directory, FontDescription description, params FontEffect[] effects) 
        => LoadFont(directory, false, description, effects);

    ///<summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
    ///<param name="directory"> The relative path to place the font textures. </param>
    ///<param name="asAsset"> Output textures in the asset library directory. </param>
    ///<param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
    ///<param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
    ///<exception cref="InvalidOperationException"/>
    public FontGenerator LoadFont(string directory, bool asAsset, FontDescription description, params FontEffect[] effects)
    {
        var assetDirectory = asAsset ? context.ProjectAssetPath : context.MapsetPath;
        var fontDirectory = Path.GetFullPath(Path.Combine(assetDirectory, directory));
        if (fonts.ContainsKey(fontDirectory)) throw new InvalidOperationException($"This effect already generated a font inside \"{fontDirectory}\"");

        if (Directory.Exists(fontDirectory)) foreach (var file in Directory.GetFiles(fontDirectory, "*.png")) PathHelper.SafeDelete(file);
        else Directory.CreateDirectory(fontDirectory);

        FontGenerator fontGenerator = new(directory, description, effects, context.ProjectPath, assetDirectory);
        fonts.Add(fontDirectory, fontGenerator);

        var cachePath = $"{fontCacheDirectory}/font.dat";
        if (File.Exists(cachePath)) using (FileStream file = new(cachePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) 
        using (ZipArchive cache = new(file, ZipArchiveMode.Read))
        {
            var path = cache.GetEntry(HashHelper.GetMd5(directory));
            if (path is not null)
            {
                var cachedFontRoot = Misc.WithRetries(() => TinyToken.Read(path.Open(), TinyToken.Yaml), canThrow: false);
                if (cachedFontRoot is not null) fontGenerator.HandleCache(cachedFontRoot);
            }
        }
        return fontGenerator;
    }
    void saveFontCache()
    {
        if (!Directory.Exists(fontCacheDirectory)) Directory.CreateDirectory(fontCacheDirectory);

        using FileStream file = new($"{fontCacheDirectory}/font.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        using ZipArchive cache = new(file, ZipArchiveMode.Update);
        foreach (var fontGenerator in fonts.Values)
        {
            var fontRoot = fontGenerator.ToTinyObject();
            var path = cache.GetEntry(HashHelper.GetMd5(fontGenerator.Directory)) ?? cache.CreateEntry(HashHelper.GetMd5(fontGenerator.Directory), CompressionLevel.Optimal);
            try
            {
                fontRoot.Write(path.Open(), TinyToken.Yaml);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to save font cache for {path.Name} ({e.GetType().FullName})");
            }
        }
    }

    #endregion

    #region Configuration

    ///<summary/>
    public void UpdateConfiguration(EffectConfig config)
    {
        if (context is not null) throw new InvalidOperationException();

        var remainingFieldNames = config.FieldNames.ToList();
        foreach (var configurableField in CollectionsMarshal.AsSpan(configurableFields))
        {
            var field = configurableField.Field;
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
                    allowedValues[i] = new()
                    {
                        Name = value.ToString(),
                        Value = Convert.ChangeType(value, fieldType, CultureInfo.InvariantCulture)
                    };
                }
            }

            try
            {
                var displayName = configurableField.Attribute.DisplayName;
                var initialValue = Convert.ChangeType(configurableField.InitialValue, fieldType, CultureInfo.InvariantCulture);
                config.UpdateField(field.Name, displayName, configurableField.Description, configurableField.Order, fieldType, initialValue, allowedValues, configurableField.BeginsGroup);

                var value = config.GetValue(field.Name);
                field.SetValue(this, value);

                remainingFieldNames.Remove(field.Name);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to update configuration for {field.Name} with type {fieldType}:\n{e}");
            }
        }
        remainingFieldNames.ForEach(config.RemoveField);
    }

    ///<summary/>
    public void ApplyConfiguration(EffectConfig config)
    {
        if (context is not null) throw new InvalidOperationException();

        foreach (var configurableField in CollectionsMarshal.AsSpan(configurableFields))
        {
            var field = configurableField.Field;
            try
            {
                var value = config.GetValue(field.Name);
                field.SetValue(this, value);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to apply configuration for {field.Name}:\n{e}");
            }
        }
    }

    struct ConfigurableField(FieldInfo field, ConfigurableAttribute attribute, object initialValue, string beginsGroup, string description, int order)
    {
        internal FieldInfo Field = field;
        internal ConfigurableAttribute Attribute = attribute;
        internal object InitialValue = initialValue;
        internal string BeginsGroup = beginsGroup, Description = description;
        internal int Order = order;

        public override readonly string ToString() => $"{Field.Name} {InitialValue}";
    }

    #endregion

    ///<summary> Generates the storyboard created by this script. </summary>
    public void Generate(GeneratorContext context)
    {
        if (instance.Value is not null) throw new InvalidOperationException("A script is already running in this thread");
        try
        {
            this.context = context;
            rnd = new(RandomSeed);
            Compressor = new IntegratedCompressor();
            instance.Value = this;

            Generate();
            context.Multithreaded = Multithreaded;
        }
        finally
        {
            instance.Value = null;
            if (fonts.Count > 0) saveFontCache();
            this.context = null;

            bitmaps.Dispose();
            fonts.Clear();
            Compressor.Dispose();
        }
    }
    ///<summary> Main body for storyboard generation. </summary>
    protected abstract void Generate();
}