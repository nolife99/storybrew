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

namespace StorybrewCommon.Scripting
{
    ///<summary> Base abstract class for all storyboarding scripts. </summary>
    public abstract class StoryboardObjectGenerator : Script
    {
        ///<summary> A storyboard generator for non-inherited types.<para/>This value will change according to what <see cref="AppDomain"/> is accessing it. </summary>
        public static StoryboardObjectGenerator Current { get; private set; }

        List<ConfigurableField> configurableFields;
        GeneratorContext context;

        ///<summary> Set to true if this script uses multiple threads. </summary>
        ///<remarks> It will prevent other effects from updating in parallel to this one. </remarks>
        protected bool Multithreaded = false;

        internal PngCompressor Compressor { get; private set; }

        ///<summary> Creates or retrieves a layer. </summary>
        ///<remarks> The identifier will be shown in the editor as "Effect name (Identifier)". </remarks>
        public StoryboardLayer GetLayer(string name) => context.GetLayer(name);

        ///<summary> The current beatmap. </summary>
        public Beatmap Beatmap => context.Beatmap;

        ///<summary> Gets the beatmap with the specified difficulty name, or if not found, gets a default value. </summary>
        public Beatmap GetBeatmap(string name) => context.Beatmaps.FirstOrDefault(b => b.Name == name);

        ///<summary> Path to the directory of this project. </summary>
        public string ProjectPath => context.ProjectPath;

        ///<summary> Path to the asset library directory of this project. </summary>
        public string AssetPath => context.ProjectAssetPath;

        ///<summary> Path to the mapset target of this project. </summary>
        public string MapsetPath => context.MapsetPath;

        ///<summary> Constructs a new storyboard object generator. </summary>
        public StoryboardObjectGenerator() => initializeConfigurableFields();

        ///<summary> Adds a dependency at the given <paramref name="path"/>. </summary>
        public void AddDependency(string path) => context.AddDependency(path);

        ///<summary> Logs a message on the effect. </summary>
        ///<param name="message"> Message to be displayed. </param>
        public void Log(object message) => context.AppendLog(message.ToString());

        ///<summary> Throws an exception if <paramref name="condition"/> returns false. </summary>
        ///<param name="condition"> The condition to be asserted. </param>
        ///<param name="message"> The message to display if assertion fails. </param>
        ///<param name="line"> The line at which the condition should be taken into account. </param>
        public void Assert(bool condition, string message = null, [CallerLineNumber] int line = -1)
        {
            if (!condition) throw new Exception(message != null ? $"Assertion failed line {line}: {message}" : $"Assertion failed line {line}");
        }

        #region File loading

        readonly DisposableNativeDictionary<string, Bitmap> bitmaps = new DisposableNativeDictionary<string, Bitmap>();

        ///<summary> Returns a <see cref="Bitmap"/> from the project's directory. </summary>
        public Bitmap GetProjectBitmap(string path, bool watch = true) => getBitmap(Path.Combine(context.ProjectPath, path), null, watch);

        ///<summary> Returns a <see cref="Bitmap"/> from the mapset's directory. </summary>
        public Bitmap GetMapsetBitmap(string path, bool watch = true) => getBitmap(Path.Combine(context.MapsetPath, path), Path.Combine(context.ProjectAssetPath, path), watch);

        Bitmap getBitmap(string path, string alternatePath, bool watch)
        {
            path = Path.GetFullPath(path);
            if (!bitmaps.TryGetValue(path, out Bitmap bitmap)) using (var stream = File.OpenRead(path))
            {
                if (watch) context.AddDependency(path);

                if (alternatePath != null && !File.Exists(path))
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
                else bitmaps[path] = bitmap = Misc.WithRetries(() => new Bitmap(stream));
            }
            return bitmap;
        }

        ///<summary> Opens a project file in read-only mode. You are responsible for disposing it. </summary>
        public Stream OpenProjectFile(string path, bool watch = true) => openFile(Path.Combine(context.ProjectPath, path), watch);

        ///<summary> Opens a mapset file in read-only mode. You are responsible for disposing it. </summary>
        public Stream OpenMapsetFile(string path, bool watch = true) => openFile(Path.Combine(context.MapsetPath, path), watch);

        Stream openFile(string path, bool watch)
        {
            path = Path.GetFullPath(path);
            if (watch) context.AddDependency(path);
            return Misc.WithRetries(() => File.OpenRead(path));
        }

        #endregion

        #region Random

        ///<summary/>
        [Group("Common")] [Description("Changes the result of Random(...) calls.")]
        [Configurable] public int RandomSeed;

        Random rnd;

        ///<summary> Gets a pseudo-random integer with minimum value <paramref name="minValue"/> and maximum value <paramref name="maxValue"/>. </summary>
        public int Random(int minValue, int maxValue) => rnd.Next(minValue, maxValue);

        ///<summary> Gets a pseudo-random integer with minimum value 0 and maximum value <paramref name="maxValue"/>. </summary>
        public int Random(int maxValue) => rnd.Next(maxValue);

        ///<summary> Gets a pseudo-random number with minimum value <paramref name="minValue"/> and maximum value <paramref name="maxValue"/>. </summary>
        public double Random(double minValue, double maxValue) => minValue + rnd.NextDouble() * (maxValue - minValue);

        ///<summary> Gets a pseudo-random number with minimum value 0 and maximum value <paramref name="maxValue"/>. </summary>
        public double Random(double maxValue) => rnd.NextDouble() * maxValue;

        ///<summary> Gets a pseudo-random float with minimum value <paramref name="minValue"/> and maximum value <paramref name="maxValue"/>. </summary>
        public float Random(float minValue, float maxValue) => (float)(minValue + rnd.NextDouble() * (maxValue - minValue));

        ///<summary> Gets a pseudo-random float with minimum value 0 and maximum value <paramref name="maxValue"/>. </summary>
        public float Random(float maxValue) => (float)(rnd.NextDouble() * maxValue);

        #endregion

        #region Audio

        ///<summary> Gets the audio duration of the beatmap in milliseconds. </summary>
        public double AudioDuration => context.AudioDuration;

        ///<summary> Gets the Fast Fourier Transform of the song at <paramref name="time"/>, with default magnitudes. </summary>
        public float[] GetFft(double time, string path = null, bool splitChannels = false)
        {
            if (!(path is null)) AddDependency(path);
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
                var index = (int)Math.Min(Math.Max(baseIndex + 1, progress * usedFftLength), usedFftLength - 1);

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

        internal readonly HashSet<string> fontDirectories = new HashSet<string>();
        readonly HashSet<FontGenerator> fontGenerators = new HashSet<FontGenerator>();

        string fontCacheDirectory => Path.Combine(context.ProjectPath, ".cache");

        ///<summary> Loads subtitles from a given subtitle file. </summary>
        public SubtitleSet LoadSubtitles(string path)
        {
            path = Path.Combine(context.ProjectPath, path);
            context.AddDependency(path);

            switch (Path.GetExtension(path))
            {
                case ".srt": return srt.Parse(path);
                case ".ssa":
                case ".ass": return ass.Parse(path);
                case ".sbv": return sbv.Parse(path);
            }
            throw new NotSupportedException($"{Path.GetExtension(path)} isn't a supported subtitle format");
        }

        ///<summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
        ///<param name="directory"> The path to the font file. </param>
        ///<param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
        ///<param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
        public FontGenerator LoadFont(string directory, FontDescription description, params FontEffect[] effects) 
            => LoadFont(directory, false, description, effects);

        ///<summary> Returns a <see cref="FontGenerator"/> to create and use textures. </summary>
        ///<param name="directory"> The path to the font file. </param>
        ///<param name="asAsset"> Whether to place textures in the asset library directory or the beatmap's storyboard directory. </param>
        ///<param name="description"> A <see cref="FontDescription"/> class with information of the texture. </param>
        ///<param name="effects"> A list of font effects, such as <see cref="FontGlow"/>. </param>
        ///<exception cref="InvalidOperationException"/>
        public FontGenerator LoadFont(string directory, bool asAsset, FontDescription description, params FontEffect[] effects)
        {
            var assetDirectory = asAsset ? context.ProjectAssetPath : context.MapsetPath;
            var fontDirectory = Path.GetFullPath(Path.Combine(assetDirectory, directory));
            if (fontDirectories.Contains(fontDirectory)) throw new InvalidOperationException($"This effect already generated a font inside \"{fontDirectory}\"");
            fontDirectories.Add(fontDirectory);

            var fontGenerator = new FontGenerator(directory, description, effects, context.ProjectPath, assetDirectory);
            fontGenerators.Add(fontGenerator);

            var cachePath = $"{fontCacheDirectory}/font.dat";
            if (File.Exists(cachePath)) using (var cache = ZipFile.OpenRead(cachePath))
            {
                var path = cache.GetEntry(HashHelper.GetMd5(fontGenerator.Directory));
                if (path != null)
                {
                    var cachedFontRoot = Misc.WithRetries(() => TinyToken.Read(path.Open(), TinyToken.Yaml), canThrow: false);
                    if (cachedFontRoot != null) fontGenerator.HandleCache(cachedFontRoot);
                }
            }
            return fontGenerator;
        }
        void saveFontCache()
        {
            if (!Directory.Exists(fontCacheDirectory)) Directory.CreateDirectory(fontCacheDirectory);

            var cachePath = $"{fontCacheDirectory}/font.dat";
            using (var file = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.Read)) using (var cache = new ZipArchive(file, ZipArchiveMode.Create))
            foreach (var fontGenerator in fontGenerators)
            {
                var fontRoot = fontGenerator.ToTinyObject();

                var path = cache.CreateEntry(HashHelper.GetMd5(fontGenerator.Directory), CompressionLevel.Optimal);
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
            if (context != null) throw new InvalidOperationException();

            var remainingFieldNames = new List<string>(config.FieldNames);
            configurableFields.ForEach(configurableField =>
            {
                var field = configurableField.Field;
                var allowedValues = (NamedValue[])null;

                var fieldType = field.FieldType;
                if (fieldType.IsEnum)
                {
                    var enumValues = Enum.GetValues(fieldType);
                    fieldType = Enum.GetUnderlyingType(fieldType);

                    allowedValues = new NamedValue[enumValues.Length];
                    for (var i = 0; i < enumValues.Length; ++i)
                    {
                        var value = enumValues.GetValue(i);
                        allowedValues[i] = new NamedValue
                        {
                            Name = value.ToString(),
                            Value = Convert.ChangeType(value, fieldType)
                        };
                    }
                }

                try
                {
                    var displayName = configurableField.Attribute.DisplayName;
                    var initialValue = Convert.ChangeType(configurableField.InitialValue, fieldType);
                    config.UpdateField(field.Name, displayName, configurableField.Description, configurableField.Order, fieldType, initialValue, allowedValues, configurableField.BeginsGroup);

                    var value = config.GetValue(field.Name);
                    field.SetValue(this, value);

                    remainingFieldNames.Remove(field.Name);
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Failed to update configuration for {field.Name} with type {fieldType}:\n{e}");
                }
            });
            remainingFieldNames.ForEach(name => config.RemoveField(name));
        }

        ///<summary/>
        public void ApplyConfiguration(EffectConfig config)
        {
            if (context != null) throw new InvalidOperationException();

            configurableFields.ForEach(configurableField =>
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
            });
        }
        void initializeConfigurableFields()
        {
            configurableFields = new List<ConfigurableField>();
            var order = 0;
            var type = GetType();

            foreach (var field in type.GetFields())
            {
                var configurable = field.GetCustomAttribute<ConfigurableAttribute>(true);
                if (configurable == null) continue;
                if (!field.FieldType.IsEnum && !ObjectSerializer.Supports(field.FieldType.FullName)) continue;

                var group = field.GetCustomAttribute<GroupAttribute>(true);
                var description = field.GetCustomAttribute<DescriptionAttribute>(true);

                configurableFields.Add(new ConfigurableField(field, configurable, field.GetValue(this), group?.Name?.Trim(), description?.Content?.Trim(), order++));
            }
        }
        struct ConfigurableField
        {
            internal FieldInfo Field;
            internal ConfigurableAttribute Attribute;
            internal object InitialValue;
            internal string BeginsGroup, Description;
            internal int Order;

            internal ConfigurableField(FieldInfo field, ConfigurableAttribute attribute, object initialValue, string beginsGroup, string description, int order)
            {
                Field = field;
                Attribute = attribute;
                InitialValue = initialValue;
                BeginsGroup = beginsGroup;
                Description = description;
                Order = order;
            }

            public override string ToString() => $"{Field.Name} {InitialValue}";
        }

        #endregion

        ///<summary> Generates the storyboard created by this script. </summary>
        public void Generate(GeneratorContext context)
        {
            if (Current != null) throw new InvalidOperationException("A script is already running in this domain");
            try
            {
                this.context = context;
                rnd = new Random(RandomSeed);
                Current = this;
                Compressor = new PngCompressor();

                Generate();
                context.Multithreaded = Multithreaded;
                if (fontGenerators.Count > 0) saveFontCache();
            }
            finally
            {
                this.context = null;
                Current = null;

                fontGenerators.Clear();
                bitmaps.Dispose();
            }
        }
        ///<summary> Main body for storyboard generation. </summary>
        protected abstract void Generate();
    }
}