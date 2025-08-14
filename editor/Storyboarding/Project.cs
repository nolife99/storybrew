namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Textures;
using BrewLib.IO;
using BrewLib.Memory;
using BrewLib.Util;
using OpenTK.Mathematics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;
using StorybrewEditor.Mapset;
using StorybrewEditor.Scripting;
using StorybrewEditor.Util;
using Tiny;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;
using ZLinq;
using Path = System.IO.Path;

public sealed partial class Project : IDisposable
{
    const string BinaryExtension = ".sbp", TextExtension = ".sbrew.yaml",
        DefaultBinaryFilename = "project" + BinaryExtension, DefaultTextFilename = "project" + TextExtension,
        DataFolder = ".sbrew";

    public static readonly string ProjectsFolder = Path.GetFullPath("projects");

    public static readonly KeyValuePair<string, string>[] FileFilter =
    [
        new("project files", string.Join(',', BinaryExtension.TrimStart('.'), TextExtension.TrimStart('.')))
    ];

    public static readonly Encoding Encoding = Encoding.ASCII;
    readonly string CommonScriptsPath, projectPath;
    readonly ScriptManager<StoryboardObjectGenerator> scriptManager;

    internal bool DisplayDebugWarning, ShowHitObjects;

    string projectFolderPath, projectAssetFolderPath;

    Project(string projectPath, bool withCommonScripts, ResourceContainer resourceContainer)
    {
        this.projectPath = projectPath;

        reloadTextures();
        reloadAudio();

        ScriptsPath = Path.GetDirectoryName(projectPath);
        if (withCommonScripts)
        {
            CommonScriptsPath = Path.GetFullPath("../../../scripts");
            if (!Directory.Exists(CommonScriptsPath))
            {
                CommonScriptsPath = Path.GetFullPath("scripts");
                if (!Directory.Exists(CommonScriptsPath)) Directory.CreateDirectory(CommonScriptsPath);
            }
        }

        var scriptsLibraryPath = Path.Combine(ScriptsPath, "scriptslibrary");
        if (!Directory.Exists(scriptsLibraryPath)) Directory.CreateDirectory(scriptsLibraryPath);

        Trace.WriteLine($"Scripts path - project:{ScriptsPath}, common:{CommonScriptsPath}, library:{scriptsLibraryPath
        }");

        initializeAssetWatcher();
        using (var referencedAss = DefaultAssemblies.AsValueEnumerable()
            .Union(ImportedAssemblies.AsValueEnumerable())
            .ToArrayPool())
            scriptManager = new(resourceContainer,
                "StorybrewScripts",
                ScriptsPath,
                CommonScriptsPath,
                scriptsLibraryPath,
                referencedAss.Span);

        effectUpdateQueue.OnActionFailed += (effect, e)
            => Trace.TraceError($"'{effect}' action: {e.GetType()} ({e.Message})");

        LayerManager.OnLayersChanged += (_, _) => Changed = true;
        OnMainBeatmapChanged += sender =>
        {
            foreach (var effect in sender.effects)
                if (effect.BeatmapDependent)
                    sender.QueueEffectUpdate(effect);
        };
    }

    public ExportSettings ExportSettings { get; } = new();
    public LayerManager LayerManager { get; } = new();
    public string ScriptsPath { get; }

    public string ProjectFolderPath => projectFolderPath ??= Path.GetDirectoryName(projectPath);

    public string ProjectAssetFolderPath => projectAssetFolderPath ??= Path.Combine(ProjectFolderPath, "assetlibrary");

    public string AudioPath
    {
        get
        {
            if (!Directory.Exists(MapsetPath)) return null;

            foreach (var beatmap in MapsetManager.Beatmaps)
            {
                if (beatmap.AudioFilename is null) continue;

                var path = Path.Combine(MapsetPath, beatmap.AudioFilename);
                if (!File.Exists(path)) continue;

                return path;
            }

            return Directory.EnumerateFiles(MapsetPath, "*.mp3", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }
    }

    public string OsbPath
    {
        get
        {
            if (!MapsetPathIsValid) return Path.Combine(ProjectFolderPath, "storyboard.osb");

            var regex = OsuFileRegex();
            var osuFilename = Path.GetFileName(MainBeatmap.Path.AsSpan());

            if (regex.IsMatch(osuFilename))
            {
                var match = regex.Match(osuFilename.ToString());
                return Path.Combine(MapsetPath, string.Concat(match.Groups[1].ValueSpan, ".osb"));
            }

            foreach (var osbFilePath in Directory.EnumerateFiles(MapsetPath, "*.osb", SearchOption.TopDirectoryOnly))
                return osbFilePath;

            return Path.Combine(MapsetPath, "storyboard.osb");
        }
    }

    [GeneratedRegex(@"^(.+ - .+ \(.+\)) \[.+\].osu$")]
    private static partial Regex OsuFileRegex();

    #region Audio and Display

    public static readonly OsbLayer[] OsbLayers =
    [
        OsbLayer.Background, OsbLayer.Fail, OsbLayer.Pass, OsbLayer.Foreground, OsbLayer.Overlay
    ];

    public float DisplayTime { get; internal set; }
    public float DimFactor { get; internal set; }

    public TextureContainer TextureContainer { get; private set; }
    public AudioSampleContainer AudioContainer { get; private set; }

    public readonly FrameStats FrameStats = new();

    public void TriggerEvents(float startTime, float endTime) => LayerManager.TriggerEvents(startTime, endTime);

    public void Draw(DrawContext drawContext, ICamera camera, RectangleF bounds, float opacity, bool updateFrameStats)
    {
        effectUpdateQueue.Enabled = allowEffectUpdates && MapsetPathIsValid;

        var newStats = updateFrameStats ? FrameStats : null;
        if (updateFrameStats)
        {
            newStats.LoadedPaths.Clear();
            newStats.OverlappedSprites.Clear();
            newStats.IncompatibleSprites.Clear();
            newStats.ProlongedSprites.Clear();

            newStats.GpuPixelsFrame = 0;
            newStats.LastBlendingMode = false;

            newStats.LastTexture = null;
            newStats.ScreenFill = 0;
            newStats.SpriteCount = newStats.Batches = newStats.CommandCount = newStats.EffectiveCommandCount = 0;
        }

        LayerManager.Draw(drawContext, camera, bounds, opacity, newStats);
    }

    void reloadTextures()
    {
        TextureContainer?.Dispose();
        TextureContainer = new TextureContainerSeparate();
    }

    void reloadAudio()
    {
        AudioContainer?.Dispose();
        AudioContainer = new(Program.AudioManager);
    }

    Task reloadTask;

    void runReload()
    {
        if (reloadTask is not null && !reloadTask.IsCompleted) return;

        reloadTask = Task.Factory.StartNew(async project =>
            {
                var p = (Project)project;
                while (p.effectUpdateQueue.Running) await Task.Delay(200);

                await Program.Schedule(proj =>
                    {
                        if (proj.isReloadingTextures)
                        {
                            proj.reloadTextures();
                            proj.isReloadingTextures = false;
                        }

                        if (proj.isReloadingAudio)
                        {
                            proj.reloadAudio();
                            proj.isReloadingAudio = false;
                        }
                    },
                    (Project)project);
            },
            this);
    }

    #endregion

    #region Effects

    readonly PooledList<Effect> effects = [];
    public ReadOnlySpan<Effect> Effects => effects.AsReadOnlySpan();

    public event EventHandler OnEffectsChanged, OnEffectsStatusChanged, OnEffectsContentChanged;

    public EffectStatus EffectsStatus { get; private set; } = EffectStatus.Initializing;

    public float StartTime => effects.Count > 0 ? effects.Min(e => e.StartTime) : 0;

    public float EndTime => effects.Count > 0 ? effects.Max(e => e.EndTime) : 0;

    bool allowEffectUpdates = true;

    readonly AsyncActionQueue<Effect> effectUpdateQueue = new(true, Program.Settings.EffectThreads);

    public void QueueEffectUpdate(Effect effect)
    {
        effectUpdateQueue.Queue(effect,
            string.GetHashCode(effect.Path, StringComparison.OrdinalIgnoreCase),
            effect.Update,
            effect.Multithreaded);

        refreshEffectsStatus();
    }

    public Task CancelEffectUpdates(bool stopThreads) => effectUpdateQueue.CancelQueuedActions(stopThreads);

    public void StopEffectUpdates()
    {
        allowEffectUpdates = false;
        effectUpdateQueue.Enabled = false;
    }

    public IEnumerable<string> GetEffectNames() => scriptManager.GetScriptNames();

    public Effect AddScriptedEffect(ReadOnlySpan<char> scriptName, bool multithreaded = false)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        ScriptedEffect effect =
            new(this, scriptManager.Get(scriptName), multithreaded) { Name = GetUniqueEffectName(scriptName) };

        effects.Add(effect);
        Changed = true;

        effect.Changed += EffectChanged;
        refreshEffectsStatus();

        OnEffectsChanged?.Invoke(this, EventArgs.Empty);
        QueueEffectUpdate(effect);

        return effect;
    }

    public void Remove(Effect effect)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        effects.Remove(effect);
        effect.Dispose();
        Changed = true;

        refreshEffectsStatus();

        OnEffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    string GetUniqueEffectName(scoped ReadOnlySpan<char> baseName)
    {
        var count = 0;
        string name;
        do name = $"{baseName} {++count}";
        while (effects.Exists(e => e.Name.SequenceEqual(name)));

        return name;
    }

    void EffectChanged(Effect sender)
    {
        if (Disposed) return;

        Changed = true;

        refreshEffectsStatus();
        OnEffectsContentChanged?.Invoke(this, EventArgs.Empty);
    }

    void refreshEffectsStatus()
    {
        var previousStatus = EffectsStatus;
        var isUpdating = effectUpdateQueue is not null && effectUpdateQueue.Running;

        var hasError = false;

        foreach (var effect in effects)
            switch (effect.Status)
            {
                case EffectStatus.Loading:
                case EffectStatus.Configuring:
                case EffectStatus.Updating:
                case EffectStatus.ReloadPending: isUpdating = true; break;

                case EffectStatus.CompilationFailed:
                case EffectStatus.LoadingFailed:
                case EffectStatus.ExecutionFailed: hasError = true; break;

                case EffectStatus.Initializing:
                case EffectStatus.Ready:
                case EffectStatus.UpdateCanceled: break;
            }

        EffectsStatus = hasError ? EffectStatus.ExecutionFailed :
            isUpdating ? EffectStatus.Updating : EffectStatus.Ready;

        if (EffectsStatus != previousStatus) OnEffectsStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Mapset

    public bool MapsetPathIsValid { get; private set; }

    string mapsetPath;

    public string MapsetPath
    {
        get => mapsetPath;
        set
        {
            if (mapsetPath == value) return;

            mapsetPath = value;
            MapsetPathIsValid = Directory.Exists(mapsetPath);
            Changed = true;

            OnMapsetPathChanged?.Invoke(this, EventArgs.Empty);
            refreshMapset();
        }
    }

    public event EventHandler OnMapsetPathChanged;
    public MapsetManager MapsetManager { get; private set; }

    EditorBeatmap mainBeatmap;

    public EditorBeatmap MainBeatmap
    {
        get
        {
            if (mainBeatmap is null) SwitchMainBeatmap();
            return mainBeatmap;
        }
        set
        {
            if (mainBeatmap == value) return;

            mainBeatmap = value;
            Changed = true;

            OnMainBeatmapChanged?.Invoke(this);
        }
    }

    public event Action<Project> OnMainBeatmapChanged;

    public void SwitchMainBeatmap()
    {
        var takeNextBeatmap = false;
        foreach (var beatmap in MapsetManager.Beatmaps)
            if (takeNextBeatmap)
            {
                MainBeatmap = beatmap;
                return;
            }
            else if (beatmap == mainBeatmap) takeNextBeatmap = true;

        foreach (var beatmap in MapsetManager.Beatmaps)
        {
            MainBeatmap = beatmap;
            return;
        }

        MainBeatmap = new(null);
    }

    public void SelectBeatmap(long id, string name)
    {
        foreach (var beatmap in MapsetManager.Beatmaps)
            if (id > 0 && beatmap.Id == id || name.Length > 0 && beatmap.Name == name)
            {
                MainBeatmap = beatmap;
                break;
            }
    }

    void refreshMapset()
    {
        var previousBeatmapId = mainBeatmap?.Id ?? -1;
        var previousBeatmapName = mainBeatmap?.Name;

        mainBeatmap = null;
        MapsetManager?.Dispose();

        MapsetManager = new(mapsetPath, MapsetManager is not null);
        MapsetManager.OnFileChanged += mapsetManager_OnFileChanged;

        if (previousBeatmapName is not null) SelectBeatmap(previousBeatmapId, previousBeatmapName);
    }

    bool isReloadingTextures, isReloadingAudio;

    void mapsetManager_OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (Disposed) return;

        switch (Path.GetExtension(e.Name.AsSpan()))
        {
            case ".png" or ".jpg" or ".jpeg" when isReloadingTextures:
            case ".wav" or ".mp3" or ".ogg" when isReloadingAudio: return;

            case ".png" or ".jpg" or ".jpeg": isReloadingTextures = true; break;
            case ".wav" or ".mp3" or ".ogg": isReloadingAudio = true; break;

            case ".osu":
                refreshMapset();
                return;

            default: return;
        }

        runReload();
    }

    #endregion

    #region Asset library folder

    FileSystemWatcher assetWatcher;

    void initializeAssetWatcher()
    {
        var assetsFolderPath = Path.GetFullPath(ProjectAssetFolderPath);
        if (!Directory.Exists(assetsFolderPath)) Directory.CreateDirectory(assetsFolderPath);

        assetWatcher = new() { Path = assetsFolderPath, IncludeSubdirectories = true, NotifyFilter = NotifyFilters.Size };

        assetWatcher.Created += assetWatcher_OnFileChanged;
        assetWatcher.Changed += assetWatcher_OnFileChanged;
        assetWatcher.Renamed += assetWatcher_OnFileChanged;
        assetWatcher.Error += (_, e) => Trace.TraceError($"Watcher (assets): {e.GetException()}");

        assetWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (assets): {assetsFolderPath}");
    }

    void assetWatcher_OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (Disposed) return;

        switch (Path.GetExtension(e.Name.AsSpan()))
        {
            case ".png" or ".jpg" or ".jpeg" when isReloadingTextures:
            case ".wav" or ".mp3" or ".ogg" when isReloadingAudio: return;

            case ".png" or ".jpg" or ".jpeg": isReloadingTextures = true; break;
            case ".wav" or ".mp3" or ".ogg": isReloadingAudio = true; break;

            default: return;
        }

        runReload();
    }

    #endregion

    #region Assemblies

    static readonly CompositeFormat runtimePath = CompositeFormat.Parse(Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(),
        "../../../packs/{0}",
        Environment.Version.ToString(),
        "ref"));

    public static readonly string RuntimeRefDirectory =
        string.Format(CultureInfo.InvariantCulture, runtimePath, "Microsoft.NETCore.App.Ref");

    public static readonly string[] DefaultAssemblies =
    [
        typeof(Font).Assembly.Location,
        typeof(IPathCollection).Assembly.Location,
        typeof(Rgba32).Assembly.Location,
        typeof(MathHelper).Assembly.Location,
        typeof(Script).Assembly.Location,
        typeof(ValueArray<>).Assembly.Location,
        typeof(Pool<>).Assembly.Location,
        .. Directory.EnumerateFiles(RuntimeRefDirectory, "*.dll", SearchOption.AllDirectories)
    ];

    readonly PooledList<string> importedAssemblies = [];

    public ReadOnlySpan<string> ImportedAssemblies
    {
        get => importedAssemblies.AsReadOnlySpan();
        set
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            importedAssemblies.Clear();

            using var distinctAss = value.AsValueEnumerable().Distinct().ToArrayPool();
            using var referencedAss =
                DefaultAssemblies.AsValueEnumerable().Union(distinctAss.AsValueEnumerable()).ToArrayPool();

            scriptManager.ReferencedAssemblies = referencedAss.Span;
            importedAssemblies.AddRange(distinctAss.Span);
        }
    }

    #endregion

    #region Save / Load / Export

    const int Version = 9;
    public bool Changed { get; private set; }

    bool ownsOsb;

    bool OwnsOsb
    {
        get => ownsOsb;
        set
        {
            if (ownsOsb == value) return;

            ownsOsb = value;
            Changed = true;
        }
    }

    public ValueTask Save()
    {
        var text = projectPath.Replace(DefaultBinaryFilename, DefaultTextFilename);

        return File.Exists(text) ?
            saveText(text) :
            saveBinary(projectPath.Replace(DefaultTextFilename, DefaultBinaryFilename));
    }

    public static Project Load(string projectPath, bool withCommonScripts, ResourceContainer resourceContainer)
    {
        Project project = new(projectPath, withCommonScripts, resourceContainer);

        if (projectPath.EndsWith(BinaryExtension, StringComparison.Ordinal)) project.loadBinary(projectPath);
        else project.loadText(projectPath.Replace(DefaultBinaryFilename, DefaultTextFilename));

        return project;
    }

    ValueTask saveBinary(string path)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        BinaryWriter w = new(new BrotliStream(File.Create(path), CompressionLevel.SmallestSize, false), Encoding, false);

        w.Write(Version);

        w.Write(MapsetPath);
        w.Write(MainBeatmap.Id);
        w.Write(MainBeatmap.Name);

        w.Write(OwnsOsb);

        w.Write(effects.Count);
        foreach (var effect in effects)
        {
            w.Write7BitEncodedInt(effect.BaseName.Length);
            w.Write(effect.BaseName);

            w.Write(effect.Multithreaded);
            w.Write7BitEncodedInt(effect.Name.Length);
            w.Write(effect.Name);

            w.Write(effect.Config.Fields.Count);
            foreach (var field in effect.Config.SortedFields)
            {
                w.Write(field.Name);
                w.Write(field.DisplayName);
                ObjectSerializer.Write(w, field.Value);

                w.Write(field.AllowedValues?.Length ?? 0);
                if (field.AllowedValues is null) continue;

                foreach (var t in field.AllowedValues)
                {
                    w.Write(t.Name);
                    ObjectSerializer.Write(w, t.Value);
                }
            }
        }

        w.Write(LayerManager.LayersCount);
        foreach (var layer in LayerManager.Layers)
        {
            w.Write(layer.Name);
            w.Write(effects.IndexOf(layer.Effect));
            w.Write(layer.DiffSpecific);
            w.Write((int)layer.OsbLayer);
            w.Write(layer.Visible);
        }

        w.Write(importedAssemblies.Count);
        foreach (var assembly in importedAssemblies) w.Write(assembly);

        Changed = false;

        return w.DisposeAsync();
    }

    void loadBinary(string path)
    {
        using BinaryReader r = new(new BrotliStream(File.OpenRead(path), CompressionMode.Decompress, false),
            Encoding,
            false);

        var version = r.ReadInt32();
        if (version > Version)
            throw new InvalidOperationException("This project was saved with a newer version; you need to update.");

        MapsetPath = r.ReadString();
        SelectBeatmap(r.ReadInt64(), r.ReadString());

        OwnsOsb = r.ReadBoolean();

        var effectCount = r.ReadInt32();
        Span<char> charBuffer = stackalloc char[255];

        for (var effectIndex = 0; effectIndex < effectCount; ++effectIndex)
        {
            if (version < 8) r.ReadBytes(16);
            var effectBaseName = charBuffer[..r.Read7BitEncodedInt()];

            var read = r.Read(effectBaseName);
            if (read != effectBaseName.Length)
                throw new InvalidDataException($"Corrupted project: expected {effectBaseName.Length} characters got {read}");

            var effect = AddScriptedEffect(effectBaseName, r.ReadBoolean());
            var effectName = charBuffer[..r.Read7BitEncodedInt()];

            read = r.Read(effectName);
            if (read != effectName.Length)
                throw new InvalidDataException($"Corrupted project: expected {effectName.Length} characters got {read}");

            using (var temp = TempArray.Create<char>(effectName)) effect.Name = temp.AsReadOnlySpan();

            var fieldCount = r.ReadInt32();
            for (var fieldIndex = 0; fieldIndex < fieldCount; ++fieldIndex)
            {
                var fieldName = r.ReadString();
                var fieldDisplayName = r.ReadString();
                var fieldValue = ObjectSerializer.Read(r);

                var allowedValueCount = r.ReadInt32();
                var allowedValues = allowedValueCount > 0 ? new NamedValue[allowedValueCount] : [];

                for (var allowedValueIndex = 0; allowedValueIndex < allowedValues.Length; ++allowedValueIndex)
                    allowedValues[allowedValueIndex] = new(r.ReadString(), ObjectSerializer.Read(r));

                effect.Config.UpdateField(fieldName,
                    fieldDisplayName,
                    null,
                    fieldIndex,
                    fieldValue?.GetType(),
                    fieldValue,
                    allowedValues,
                    null);
            }
        }

        var layerCount = r.ReadInt32();
        for (var layerIndex = 0; layerIndex < layerCount; ++layerIndex)
        {
            if (version < 8) r.ReadBytes(16);
            var name = r.ReadString();

            var effect = effects[r.ReadInt32()];
            effect.AddPlaceholder(new(name, effect)
            {
                DiffSpecific = r.ReadBoolean(), OsbLayer = (OsbLayer)r.ReadInt32(), Visible = r.ReadBoolean()
            });
        }

        using var imported = ValueEnumerable.Range(0, r.ReadInt32()).Select(_ => r.ReadString()).Distinct().ToArrayPool();

        ImportedAssemblies = imported.Span;
    }

    async ValueTask saveText(string path)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        if (!File.Exists(path))
            await File.WriteAllTextAsync(path,
                "# This file is used to open the project\n# Project data is contained in /.sbrew");

        var projectDirectory = Path.GetDirectoryName(path.AsSpan());

        var gitIgnorePath = Path.Join(projectDirectory, ".gitignore");
        var targetDirectory = Path.Join(projectDirectory, DataFolder);

        if (!File.Exists(gitIgnorePath))
            await File.WriteAllTextAsync(gitIgnorePath, ".sbrew/user.yaml\n.sbrew.tmp\n.sbrew.bak\n.cache\n.vs");

        using SafeDirectoryWriter directoryWriter = new(targetDirectory);
        TinyObject indexRoot = new()
        {
            { "FormatVersion", Version },
            { "BeatmapId", MainBeatmap.Id },
            { "BeatmapName", MainBeatmap.Name },
            { "Assemblies", importedAssemblies },
            { "Layers", LayerManager.Layers.Select(l => StringHelper.GetMd5(l.Identifier)) }
        };

        var indexPath = directoryWriter.GetPath("index.yaml");
        indexRoot.Write(indexPath);

        TinyObject userRoot = new()
        {
            { "FormatVersion", Version },
            { "Editor", Program.FullName },
            { "MapsetPath", PathHelper.WithStandardSeparators(MapsetPath) },
            { "ExportTimeAsFloatingPoint", ExportSettings.UseFloatForTime },
            { "OwnsOsb", OwnsOsb }
        };

        var userPath = directoryWriter.GetPath("user.yaml");
        userRoot.Write(userPath);

        foreach (var effect in effects)
        {
            TinyObject effectRoot = new()
            {
                { "FormatVersion", Version },
                { "Name", effect.Name.ToString() },
                { "Script", effect.BaseName.ToString() },
                { "Multithreaded", effect.Multithreaded }
            };

            TinyObject configRoot = [];
            effectRoot.Add("Config", configRoot);

            foreach (var field in effect.Config.SortedFields)
            {
                TinyObject fieldRoot = new()
                {
                    { "Type", field.Type.FullName }, { "Value", ObjectSerializer.ToString(field.Type, field.Value) }
                };

                if (field.DisplayName != field.Name) fieldRoot.Add("DisplayName", field.DisplayName);

                if (!string.IsNullOrWhiteSpace(field.BeginsGroup)) fieldRoot.Add("BeginsGroup", field.BeginsGroup);

                configRoot.Add(field.Name, fieldRoot);

                if ((field.AllowedValues?.Length ?? 0) <= 0) continue;

                TinyObject allowedValuesRoot = [];
                fieldRoot.Add("AllowedValues", allowedValuesRoot);

                foreach (var allowedValue in field.AllowedValues)
                    allowedValuesRoot.Add(allowedValue.Name, ObjectSerializer.ToString(field.Type, allowedValue.Value));
            }

            TinyObject layersRoot = [];
            effectRoot.Add("Layers", layersRoot);

            foreach (var layer in LayerManager.Layers)
                if (layer.Effect == effect)
                {
                    TinyObject layerRoot = new()
                    {
                        { "Name", layer.Name },
                        { "OsbLayer", layer.OsbLayer },
                        { "DiffSpecific", layer.DiffSpecific },
                        { "Visible", layer.Visible }
                    };

                    layersRoot.Add(StringHelper.GetMd5(layer.Identifier), layerRoot);
                }

            effectRoot.Write(directoryWriter.GetPath("effect." + StringHelper.GetMd5(effect.Name) + ".yaml"));
        }

        directoryWriter.Commit();
        Changed = false;
    }

    void loadText(scoped ReadOnlySpan<char> path)
    {
        var targetDirectory = Path.Join(Path.GetDirectoryName(path), DataFolder);

        SafeDirectoryReader directoryReader = new(targetDirectory);
        var indexPath = directoryReader.GetPath("index.yaml");
        var indexRoot = TinyToken.Read(indexPath);

        var indexVersion = indexRoot.Value<int>("FormatVersion");
        if (indexVersion > Version)
            throw new InvalidOperationException("This project was saved with a newer version; you need to update.");

        var userPath = directoryReader.GetPath("user.yaml");
        TinyToken userRoot = null;
        if (File.Exists(userPath))
        {
            userRoot = TinyToken.Read(userPath);

            var userVersion = userRoot.Value<int>("FormatVersion");
            if (userVersion > Version)
                throw new InvalidOperationException(
                    "This project's user settings were saved with a newer version; you need to update.");

            ExportSettings.UseFloatForTime = userRoot.Value<bool>("ExportTimeAsFloatingPoint");

            OwnsOsb = userRoot.Value<bool>("OwnsOsb");
        }

        MapsetPath = userRoot?.Value<string>("MapsetPath") ?? indexRoot.Value<string>("MapsetPath") ?? "nul";

        SelectBeatmap(indexRoot.Value<long>("BeatmapId"), indexRoot.Value<string>("BeatmapName"));

        using (var assemblies = indexRoot.Values<string>("Assemblies").AsValueEnumerable().ToArrayPool())
            ImportedAssemblies = assemblies.Span;

        // Load effects
        using PooledDictionary<string, Action> layerInserters = new();
        foreach (var effectPath in Directory.EnumerateFiles(directoryReader.Path,
            "effect.*.yaml",
            SearchOption.TopDirectoryOnly))
        {
            var effectRoot = TinyToken.Read(effectPath);

            var effectVersion = effectRoot.Value<int>("FormatVersion");
            if (effectVersion > Version)
                throw new InvalidOperationException(
                    "This project has an effect that was saved with a newer version; you need to update.");

            var effect = AddScriptedEffect(effectRoot.Value<string>("Script"), effectRoot.Value<bool>("Multithreaded"));

            effect.Name = effectRoot.Value<string>("Name");

            var configRoot = effectRoot.Value<TinyObject>("Config");
            var fieldIndex = 0;

            foreach (var (key, fieldRoot) in configRoot)
            {
                var fieldTypeName = fieldRoot.Value<string>("Type");
                var fieldValue = ObjectSerializer.FromString(fieldTypeName, fieldRoot.Value<string>("Value"));

                effect.Config.UpdateField(key,
                    fieldRoot.Value<string>("DisplayName"),
                    null,
                    fieldIndex++,
                    fieldValue?.GetType(),
                    fieldValue,
                    fieldRoot.Value<TinyObject>("AllowedValues")
                        ?.Select(p => new NamedValue(p.Key,
                            ObjectSerializer.FromString(fieldTypeName, p.Value.Value<string>())))
                        .ToArray(),
                    fieldRoot.Value<string>("BeginsGroup"));
            }

            var layersRoot = effectRoot.Value<TinyObject>("Layers");
            foreach (var (layerHash, layerRoot) in layersRoot)
                layerInserters[layerHash] = () => effect.AddPlaceholder(new(layerRoot.Value<string>("Name"), effect)
                {
                    OsbLayer = layerRoot.Value<OsbLayer>("OsbLayer"),
                    DiffSpecific = layerRoot.Value<bool>("DiffSpecific"),
                    Visible = layerRoot.Value<bool>("Visible")
                });
        }

        if (effects.Count == 0) EffectsStatus = EffectStatus.Ready;

        var layersOrder = indexRoot.Values<string>("Layers").AsValueEnumerable().Distinct();

        foreach (var layerGuid in layersOrder)
            if (layerInserters.TryGetValue(layerGuid, out var insertLayer))
                insertLayer();

        foreach (var key in layerInserters.Keys.AsValueEnumerable().Except(layersOrder)) layerInserters[key]();
    }

    public static async ValueTask<Project> Create(string projectFolderName,
        string mapsetPath,
        bool withCommonScripts,
        ResourceContainer resourceContainer)
    {
        var project = create(projectFolderName, mapsetPath, withCommonScripts, resourceContainer);
        await project.Save();
        return project;
    }

    static Project create(scoped ReadOnlySpan<char> projectFolderName,
        string mapsetPath,
        bool withCommonScripts,
        ResourceContainer resourceContainer)
    {
        if (!Directory.Exists(ProjectsFolder)) Directory.CreateDirectory(ProjectsFolder);

        if (projectFolderName.ContainsAny(Path.GetInvalidFileNameChars()) || projectFolderName.IsWhiteSpace())
            throw new InvalidOperationException($"'{projectFolderName}' isn't a valid project folder name");

        var projectFolderPath = Path.Join(ProjectsFolder, projectFolderName);
        if (Directory.Exists(projectFolderPath))
            throw new InvalidOperationException($"A project already exists at '{projectFolderPath}'");

        Directory.CreateDirectory(projectFolderPath);
        return new(Path.Combine(projectFolderPath, DefaultBinaryFilename), withCommonScripts, resourceContainer)
        {
            MapsetPath = mapsetPath
        };
    }

    public async ValueTask ExportToOsb(bool exportOsb = true)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        string osuPath = null, osbPath = null;
        PooledList<EditorStoryboardLayer> localLayers = null, diffSpecific = null;

        await Program.Schedule(proj =>
            {
                osuPath = proj.MainBeatmap.Path;
                osbPath = proj.OsbPath;

                if (!proj.OwnsOsb && File.Exists(osbPath)) File.Move(osbPath, $"{osbPath}.bak");

                if (!proj.OwnsOsb) proj.OwnsOsb = true;

                localLayers = proj.LayerManager.FindLayers(l => l.Visible);
                diffSpecific = proj.LayerManager.FindLayers(l => l.DiffSpecific);
            },
            this);

        var usesOverlayLayer = localLayers.Exists(l => l.OsbLayer is OsbLayer.Overlay);
        using var sbLayer = localLayers.FindAll(l => !l.DiffSpecific);

        localLayers.Dispose();

        if (!string.IsNullOrEmpty(osuPath) && diffSpecific.Count != 0)
        {
            Trace.WriteLine($"Exporting diff specific events to {osuPath}");
            await using SafeWriteStream stream = new(osuPath);
            await using StreamWriter writer = new(stream, Encoding, leaveOpen: true);
            using StreamReader reader = new(osuPath, Encoding);
            var inEvents = false;
            var inStoryboard = false;

            while (await reader.ReadLineAsync() is { } line)
            {
                var trimmedLine = line.AsSpan().Trim();
                if (!inEvents && trimmedLine is "[Events]") inEvents = true;
                else if (trimmedLine.Length == 0) inEvents = false;

                if (inEvents)
                {
                    if (trimmedLine.StartsWith("//Storyboard Layer", StringComparison.Ordinal))
                    {
                        if (!inStoryboard)
                        {
                            foreach (var osbLayer in OsbLayers)
                            {
                                if (osbLayer is OsbLayer.Overlay && !usesOverlayLayer) continue;

                                await writer.WriteLineAsync($"//Storyboard Layer {(int)osbLayer} ({osbLayer})");

                                foreach (var layer in diffSpecific)
                                    if (layer.OsbLayer == osbLayer && layer.Visible)
                                        layer.WriteOsb(writer, ExportSettings);
                            }

                            inStoryboard = true;
                        }
                    }
                    else if (inStoryboard && trimmedLine.StartsWith("//", StringComparison.Ordinal)) inStoryboard = false;

                    if (inStoryboard) continue;
                }

                await writer.WriteLineAsync(line);
            }

            stream.Commit();
        }

        diffSpecific.Dispose();
        if (exportOsb && sbLayer.Count != 0)
        {
            Trace.WriteLine($"Exporting osb to {osbPath}");
            await using StreamWriter writer = new(osbPath, false, Encoding);
            await writer.WriteLineAsync("[Events]");
            await writer.WriteLineAsync("//Background and Video events");

            foreach (var osbLayer in OsbLayers)
            {
                if (osbLayer is OsbLayer.Overlay && !usesOverlayLayer) continue;

                await writer.WriteLineAsync($"//Storyboard Layer {(int)osbLayer} ({osbLayer})");

                foreach (var layer in sbLayer)
                    if (layer.OsbLayer == osbLayer)
                        layer.WriteOsb(writer, ExportSettings);
            }

            await writer.WriteLineAsync("//Storyboard Sound Samples");
        }
    }

    #endregion

    #region IDisposable Support

    public bool Disposed { get; private set; }

    public void Dispose()
    {
        if (Disposed) return;

        reloadTask?.Wait();

        effectUpdateQueue.Dispose();
        assetWatcher.Dispose();

        foreach (var effect in effects) effect.Dispose();
        effects.Clear();

        MapsetManager?.Dispose();
        scriptManager.Dispose();
        importedAssemblies.Dispose();
        TextureContainer.Dispose();
        AudioContainer.Dispose();

        LayerManager.Dispose();

        Disposed = true;
    }

    #endregion
}