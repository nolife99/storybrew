using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using BrewLib.Audio;
using BrewLib.Data;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;
using StorybrewEditor.Mapset;
using StorybrewEditor.Scripting;
using StorybrewEditor.Util;
using Tiny;

namespace StorybrewEditor.Storyboarding;

public sealed class Project : IDisposable
{
    public static readonly Encoding Encoding = Encoding.ASCII;

    public const string BinaryExtension = ".sbp", TextExtension = ".yaml",
        DefaultBinaryFilename = "project" + BinaryExtension, DefaultTextFilename = "project.sbrew" + TextExtension,
        DataFolder = ".sbrew", ProjectsFolder = "projects",
        FileFilter = "project files|" + DefaultBinaryFilename + ";" + DefaultTextFilename;

    ScriptManager<StoryboardObjectGenerator> scriptManager;

    readonly string projectPath;
    public string ProjectFolderPath => Path.GetDirectoryName(projectPath);
    public string ProjectAssetFolderPath => Path.Combine(ProjectFolderPath, "assetlibrary");

    internal bool DisplayDebugWarning, ShowHitObjects;
    public readonly string ScriptsPath, CommonScriptsPath, ScriptsLibraryPath;

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

            Regex regex = new(@"^(.+ - .+ \(.+\)) \[.+\].osu$");
            var osuFilename = Path.GetFileName(MainBeatmap.Path);

            Match match;
            if ((match = regex.Match(osuFilename)).Success) return Path.Combine(MapsetPath, $"{match.Groups[1].Value}.osb");

            foreach (var osbFilePath in Directory.EnumerateFiles(MapsetPath, "*.osb", SearchOption.TopDirectoryOnly)) return osbFilePath;

            return Path.Combine(MapsetPath, "storyboard.osb");
        }
    }

    public readonly ExportSettings ExportSettings = new();
    public readonly LayerManager LayerManager = new();

    public Project(string projectPath, bool withCommonScripts, ResourceContainer resourceContainer)
    {
        this.projectPath = projectPath;

        reloadTextures();
        reloadAudio();

        ScriptsPath = Path.GetDirectoryName(projectPath);
        if (withCommonScripts)
        {
            CommonScriptsPath = Path.GetFullPath($"../../../scripts");
            if (!Directory.Exists(CommonScriptsPath))
            {
                CommonScriptsPath = Path.GetFullPath("scripts");
                if (!Directory.Exists(CommonScriptsPath)) Directory.CreateDirectory(CommonScriptsPath);
            }
        }
        ScriptsLibraryPath = Path.Combine(ScriptsPath, "scriptslibrary");
        if (!Directory.Exists(ScriptsLibraryPath)) Directory.CreateDirectory(ScriptsLibraryPath);

        Trace.WriteLine($"Scripts path - project:{ScriptsPath}, common:{CommonScriptsPath}, library:{ScriptsLibraryPath}");

        var compiledScriptsPath = Path.GetFullPath("cache/scripts");
        if (Directory.Exists(compiledScriptsPath)) foreach (var script in Directory.EnumerateFiles(compiledScriptsPath)) PathHelper.SafeDelete(script);
        else Directory.CreateDirectory(compiledScriptsPath);

        initializeAssetWatcher();

        scriptManager = new(resourceContainer, "StorybrewScripts", ScriptsPath, CommonScriptsPath, ScriptsLibraryPath, compiledScriptsPath, ReferencedAssemblies);
        effectUpdateQueue.OnActionFailed += (effect, e) => Trace.TraceError($"'{effect}' - Action failed: {e.GetType()} ({e.Message})");

        LayerManager.OnLayersChanged += (sender, e) => Changed = true;

        OnMainBeatmapChanged += (sender, e) => effects.ForEach(effect => QueueEffectUpdate(effect), effect => effect.BeatmapDependant);
    }

    #region Audio and Display

    public static readonly OsbLayer[] OsbLayers = [OsbLayer.Background, OsbLayer.Fail, OsbLayer.Pass, OsbLayer.Foreground, OsbLayer.Overlay];

    public double DisplayTime;
    public float DimFactor;

    public TextureContainer TextureContainer;
    public AudioSampleContainer AudioContainer;

    public FrameStats FrameStats = new();

    public void TriggerEvents(double startTime, double endTime) => LayerManager.TriggerEvents(startTime, endTime);
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, bool updateFrameStats)
    {
        effectUpdateQueue.Enabled = allowEffectUpdates && MapsetPathIsValid;

        var newFrameStats = updateFrameStats ? new FrameStats() : null;
        LayerManager.Draw(drawContext, camera, bounds, opacity, newFrameStats);
        FrameStats = newFrameStats ?? FrameStats;
    }
    void reloadTextures()
    {
        TextureContainer?.Dispose();
        TextureContainer = new TextureContainerSeparate(null, TextureOptions.Default);
    }
    void reloadAudio()
    {
        AudioContainer?.Dispose();
        AudioContainer = new(Program.AudioManager, null);
    }

    #endregion

    #region Effects

    readonly List<Effect> effects = [];
    public IEnumerable<Effect> Effects => effects;
    public event EventHandler OnEffectsChanged, OnEffectsStatusChanged, OnEffectsContentChanged;

    public EffectStatus EffectsStatus = EffectStatus.Initializing;

    public double StartTime => effects.Count > 0 ? effects.Min(e => e.StartTime) : 0;
    public double EndTime => effects.Count > 0 ? effects.Max(e => e.EndTime) : 0;

    bool allowEffectUpdates = true;

    AsyncActionQueue<Effect> effectUpdateQueue = new("Effect Updates", false, Program.Settings.EffectThreads);
    public void QueueEffectUpdate(Effect effect)
    {
        effectUpdateQueue.Queue(effect, effect.Path, e => e.Update(), effect.Multithreaded);
        refreshEffectsStatus();
    }
    public void CancelEffectUpdates(bool stopThreads) => effectUpdateQueue.CancelQueuedActions(stopThreads);
    public void StopEffectUpdates()
    {
        allowEffectUpdates = false;
        effectUpdateQueue.Enabled = false;
    }

    public IEnumerable<string> GetEffectNames() => scriptManager.GetScriptNames();
    public Effect GetEffectByName(string name) => effects.Find(e => e.Name == name);

    public Effect AddScriptedEffect(string scriptName, bool multithreaded = false)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        ScriptedEffect effect = new(this, scriptManager.Get(scriptName), multithreaded)
        {
            Name = GetUniqueEffectName(scriptName)
        };

        effects.Add(effect);
        Changed = true;

        effect.OnChanged += effect_OnChanged;
        refreshEffectsStatus();

        OnEffectsChanged?.Invoke(this, EventArgs.Empty);
        QueueEffectUpdate(effect);
        return effect;
    }
    public void Remove(Effect effect)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        effects.Remove(effect);
        effect?.Dispose();
        Changed = true;

        refreshEffectsStatus();

        OnEffectsChanged?.Invoke(this, EventArgs.Empty);
    }
    public string GetUniqueEffectName(string baseName)
    {
        var count = 1;
        string name;
        do name = $"{baseName} {count++}";
        while (GetEffectByName(name) is not null);
        return name;
    }
    void effect_OnChanged(object sender, EventArgs e)
    {
        Changed = true;

        refreshEffectsStatus();
        OnEffectsContentChanged?.Invoke(this, EventArgs.Empty);
    }
    void refreshEffectsStatus()
    {
        var previousStatus = EffectsStatus;
        var isUpdating = effectUpdateQueue?.TaskCount > 0;
        var hasError = false;

        effects.ForEach(effect =>
        {
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
                case EffectStatus.Ready: break;
            }
        });
        EffectsStatus = hasError ? EffectStatus.ExecutionFailed : isUpdating ? EffectStatus.Updating : EffectStatus.Ready;
        if (EffectsStatus != previousStatus) OnEffectsStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Mapset

    public bool MapsetPathIsValid;

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
    public MapsetManager MapsetManager;

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

            OnMainBeatmapChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler OnMainBeatmapChanged;

    public void SwitchMainBeatmap()
    {
        var takeNextBeatmap = false;
        foreach (var beatmap in MapsetManager.Beatmaps)
        {
            if (takeNextBeatmap)
            {
                MainBeatmap = beatmap;
                return;
            }
            else if (beatmap == mainBeatmap) takeNextBeatmap = true;
        }
        foreach (var beatmap in MapsetManager.Beatmaps)
        {
            MainBeatmap = beatmap;
            return;
        }
        MainBeatmap = new(null);
    }
    public void SelectBeatmap(long id, string name)
    {
        foreach (var beatmap in MapsetManager.Beatmaps) if ((id > 0 && beatmap.Id == id) || (name.Length > 0 && beatmap.Name == name))
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
    void mapsetManager_OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var extension = Path.GetExtension(e.Name);
        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg") reloadTextures();
        else if (extension == ".wav" || extension == ".mp3" || extension == ".ogg") reloadAudio();
        else if (extension == ".osu") refreshMapset();
    }

    #endregion

    #region Asset library folder

    FileSystemWatcher assetWatcher;
    void initializeAssetWatcher()
    {
        var assetsFolderPath = Path.GetFullPath(ProjectAssetFolderPath);
        if (!Directory.Exists(assetsFolderPath)) Directory.CreateDirectory(assetsFolderPath);

        assetWatcher = new()
        {
            Path = assetsFolderPath,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.Size
        };
        assetWatcher.Created += assetWatcher_OnFileChanged;
        assetWatcher.Changed += assetWatcher_OnFileChanged;
        assetWatcher.Renamed += assetWatcher_OnFileChanged;
        assetWatcher.Error += (sender, e) => Trace.WriteLine($"Watcher error (assets): {e.GetException()}");
        assetWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (assets): {assetsFolderPath}");
    }
    void assetWatcher_OnFileChanged(object sender, FileSystemEventArgs e) => Program.Schedule(() =>
    {
        if (Disposed) return;

        var extension = Path.GetExtension(e.Name);
        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg") reloadTextures();
        else if (extension == ".wav" || extension == ".mp3" || extension == ".ogg") reloadAudio();
    });

    #endregion

    #region Assemblies

    public static ICollection<string> DefaultAssemblies
    {
        get
        {
            HashSet<string> distinct = [];
            foreach (var asm in AssemblyLoadContext.Default.Assemblies) if (asm.ManifestModule.Name != "<Unknown>") distinct.Add(asm.ManifestModule.Name);
            return distinct;
        }
    }

    HashSet<string> importedAssemblies = [];
    public ICollection<string> ImportedAssemblies
    {
        get => importedAssemblies;
        set
        {
            ObjectDisposedException.ThrowIf(Disposed, this);

            importedAssemblies = value as HashSet<string> ?? new(value);
            scriptManager.ReferencedAssemblies = ReferencedAssemblies;
        }
    }

    public IEnumerable<string> ReferencedAssemblies => DefaultAssemblies.Union(importedAssemblies);

    #endregion

    #region Save / Load / Export

    public const int Version = 7;
    public bool Changed;

    bool ownsOsb;
    public bool OwnsOsb
    {
        get => ownsOsb;
        set
        {
            if (ownsOsb == value) return;
            ownsOsb = value;
            Changed = true;
        }
    }

    static readonly Regex effectGuidRegex = new("effect\\.([a-z0-9]{32})\\.yaml", RegexOptions.IgnoreCase);

    public void Save()
    {
        var text = projectPath.Replace(DefaultBinaryFilename, DefaultTextFilename);
        if (File.Exists(text)) saveText(text);
        else saveBinary(projectPath.Replace(DefaultTextFilename, DefaultBinaryFilename));
    }
    public static Project Load(string projectPath, bool withCommonScripts, ResourceContainer resourceContainer)
    {
        Project project = new(projectPath, withCommonScripts, resourceContainer);
        if (projectPath.EndsWith(BinaryExtension, StringComparison.Ordinal)) project.loadBinary(projectPath);
        else project.loadText(projectPath.Replace(DefaultBinaryFilename, DefaultTextFilename));
        return project;
    }
    void saveBinary(string path)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        using var file = File.Create(path); 
        using DeflateStream dfl = new(file, CompressionLevel.Optimal); 
        using BinaryWriter w = new(dfl, Encoding);

        w.Write(Version);

        w.Write(MapsetPath);
        w.Write(MainBeatmap.Id);
        w.Write(MainBeatmap.Name);

        w.Write(OwnsOsb);

        w.Write(effects.Count);
        effects.ForEach(effect =>
        {
            w.Write(effect.Guid.ToByteArray());
            w.Write(effect.BaseName);
            w.Write(effect.Multithreaded);
            w.Write(effect.Name);

            w.Write(effect.Config.FieldCount);
            foreach (var field in effect.Config.SortedFields)
            {
                w.Write(field.Name);
                w.Write(field.DisplayName);
                ObjectSerializer.Write(w, field.Value);

                w.Write(field.AllowedValues?.Length ?? 0);
                if (field.AllowedValues is not null) for (var i = 0; i < field.AllowedValues.Length; ++i)
                {
                    w.Write(field.AllowedValues[i].Name);
                    ObjectSerializer.Write(w, field.AllowedValues[i].Value);
                }
            }
        });

        w.Write(LayerManager.LayersCount);
        foreach (var layer in LayerManager.Layers)
        {
            w.Write(layer.Guid.ToByteArray());
            w.Write(layer.Name);
            w.Write(effects.IndexOf(layer.Effect));
            w.Write(layer.DiffSpecific);
            w.Write((int)layer.OsbLayer);
            w.Write(layer.Visible);
        }

        w.Write(importedAssemblies.Count);
        foreach (var assembly in importedAssemblies) w.Write(assembly);

        Changed = false;
    }
    void loadBinary(string path)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); 
        using DeflateStream dfl = new(file, CompressionMode.Decompress); 
        using BinaryReader r = new(dfl, Encoding);

        var version = r.ReadInt32();
        if (version > Version) throw new InvalidOperationException("This project was saved with a newer version; you need to update.");

        MapsetPath = r.ReadString();
        if (version >= 1) SelectBeatmap(r.ReadInt64(), r.ReadString());

        OwnsOsb = version < 4 || r.ReadBoolean();

        var effectCount = r.ReadInt32();
        for (var effectIndex = 0; effectIndex < effectCount; ++effectIndex)
        {
            var guid = version > 5 ? new(r.ReadBytes(16)) : Guid.NewGuid();
            var effect = AddScriptedEffect(r.ReadString(), r.ReadBoolean());
            effect.Guid = guid;
            effect.Name = r.ReadString();

            if (version > 0)
            {
                var fieldCount = r.ReadInt32();
                for (var fieldIndex = 0; fieldIndex < fieldCount; ++fieldIndex)
                {
                    var fieldName = r.ReadString();
                    var fieldDisplayName = r.ReadString();
                    var fieldValue = ObjectSerializer.Read(r);

                    var allowedValueCount = r.ReadInt32();
                    var allowedValues = allowedValueCount > 0 ? new NamedValue[allowedValueCount] : null;
                    for (var allowedValueIndex = 0; allowedValueIndex < allowedValueCount; ++allowedValueIndex) allowedValues[allowedValueIndex] = new()
                    {
                        Name = r.ReadString(),
                        Value = ObjectSerializer.Read(r)
                    };

                    effect.Config.UpdateField(fieldName, fieldDisplayName, null, fieldIndex, fieldValue?.GetType(), fieldValue, allowedValues, null);
                }
            }
        }

        var layerCount = r.ReadInt32();
        for (var layerIndex = 0; layerIndex < layerCount; ++layerIndex)
        {
            var guid = version > 5 ? new(r.ReadBytes(16)) : Guid.NewGuid();
            var name = r.ReadString();

            var effect = effects[r.ReadInt32()];
            effect.AddPlaceholder(new(name, effect)
            {
                Guid = guid,
                DiffSpecific = version >= 3 && r.ReadBoolean(),
                OsbLayer = version >= 2 ? (OsbLayer)r.ReadInt32() : OsbLayer.Background,
                Visible = r.ReadBoolean()
            });
        }

        if (version > 4)
        {
            var assemblyCount = r.ReadInt32();
            var importedAssemblies = new string[assemblyCount];
            for (var i = 0; i < assemblyCount; ++i) importedAssemblies[i] = r.ReadString();

            ImportedAssemblies = importedAssemblies;
        }
    }
    void saveText(string path)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        if (!File.Exists(path)) File.WriteAllText(path, "# This file is used to open the project\n# Project data is contained in /.sbrew");

        var projectDirectory = Path.GetDirectoryName(path);

        var gitIgnorePath = Path.Combine(projectDirectory, ".gitignore");
        if (!File.Exists(gitIgnorePath)) File.WriteAllText(gitIgnorePath, ".sbrew/user.yaml\n.sbrew.tmp\n.sbrew.bak\n.cache\n.vs");

        var targetDirectory = Path.Combine(projectDirectory, DataFolder);
        using SafeDirectoryWriter directoryWriter = new(targetDirectory);
        TinyObject indexRoot = new()
        {
            { "FormatVersion", Version },
            { "BeatmapId", MainBeatmap.Id },
            { "BeatmapName", MainBeatmap.Name },
            { "Assemblies", importedAssemblies },
            { "Layers", LayerManager.Layers.Select(l => l.Guid.ToString("N")) }
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

        effects.ForEach(effect =>
        {
            TinyObject effectRoot = new()
            {
                { "FormatVersion", Version },
                { "Name", effect.Name },
                { "Script", effect.BaseName },
                { "Multithreaded", effect.Multithreaded }
            };

            TinyObject configRoot = [];
            effectRoot.Add("Config", configRoot);

            foreach (var field in effect.Config.SortedFields)
            {
                TinyObject fieldRoot = new()
                {
                    { "Type", field.Type.FullName },
                    { "Value", ObjectSerializer.ToString(field.Type, field.Value)}
                };
                if (field.DisplayName != field.Name) fieldRoot.Add("DisplayName", field.DisplayName);
                if (!string.IsNullOrWhiteSpace(field.BeginsGroup)) fieldRoot.Add("BeginsGroup", field.BeginsGroup);
                configRoot.Add(field.Name, fieldRoot);

                if ((field.AllowedValues?.Length ?? 0) > 0)
                {
                    TinyObject allowedValuesRoot = [];
                    fieldRoot.Add("AllowedValues", allowedValuesRoot);

                    foreach (var allowedValue in field.AllowedValues) allowedValuesRoot.Add(allowedValue.Name, ObjectSerializer.ToString(field.Type, allowedValue.Value));
                }
            }

            TinyObject layersRoot = [];
            effectRoot.Add("Layers", layersRoot);

            foreach (var layer in LayerManager.Layers) if (layer.Effect == effect)
            {
                TinyObject layerRoot = new()
                {
                    { "Name", layer.Name },
                    { "OsbLayer", layer.OsbLayer },
                    { "DiffSpecific", layer.DiffSpecific },
                    { "Visible", layer.Visible }
                };
                layersRoot.Add(layer.Guid.ToString("N"), layerRoot);
            }

            var effectPath = directoryWriter.GetPath("effect." + effect.Guid.ToString("N") + ".yaml");
            effectRoot.Write(effectPath);
        });

        directoryWriter.Commit(checkPaths: true);
        Changed = false;
    }

    void loadText(string path)
    {
        var targetDirectory = Path.Combine(Path.GetDirectoryName(path), DataFolder);
        SafeDirectoryReader directoryReader = new(targetDirectory);
        var indexPath = directoryReader.GetPath("index.yaml");
        var indexRoot = TinyToken.Read(indexPath);

        var indexVersion = indexRoot.Value<int>("FormatVersion");
        if (indexVersion > Version) throw new InvalidOperationException("This project was saved with a newer version; you need to update.");

        var userPath = directoryReader.GetPath("user.yaml");
        TinyToken userRoot = null;
        if (File.Exists(userPath))
        {
            userRoot = TinyToken.Read(userPath);

            var userVersion = userRoot.Value<int>("FormatVersion");
            if (userVersion > Version) throw new InvalidOperationException("This project's user settings were saved with a newer version; you need to update.");

            var savedBy = userRoot.Value<string>("Editor");

            ExportSettings.UseFloatForTime = userRoot.Value<bool>("ExportTimeAsFloatingPoint");
            OwnsOsb = userRoot.Value<bool>("OwnsOsb");
        }

        MapsetPath = userRoot?.Value<string>("MapsetPath") ?? indexRoot.Value<string>("MapsetPath") ?? "nul";
        SelectBeatmap(indexRoot.Value<long>("BeatmapId"), indexRoot.Value<string>("BeatmapName"));
        ImportedAssemblies = indexRoot.Values<string>("Assemblies").ToArray();

        // Load effects
        Dictionary<string, Action> layerInserters = [];
        foreach (var effectPath in Directory.GetFiles(directoryReader.Path, "effect.*.yaml", SearchOption.TopDirectoryOnly))
        {
            var guidMatch = effectGuidRegex.Match(effectPath);
            if (!guidMatch.Success || guidMatch.Groups.Count < 2) throw new InvalidDataException($"Could not parse effect Guid from '{effectPath}'");

            var effectRoot = TinyToken.Read(effectPath);

            var effectVersion = effectRoot.Value<int>("FormatVersion");
            if (effectVersion > Version) throw new InvalidOperationException("This project has an effect that was saved with a newer version; you need to update.");

            var effect = AddScriptedEffect(effectRoot.Value<string>("Script"), effectRoot.Value<bool>("Multithreaded"));
            effect.Guid = Guid.Parse(guidMatch.Groups[1].Value);
            effect.Name = effectRoot.Value<string>("Name");

            var configRoot = effectRoot.Value<TinyObject>("Config");
            var fieldIndex = 0;
            foreach (var fieldProperty in configRoot)
            {
                var fieldRoot = fieldProperty.Value;

                var fieldTypeName = fieldRoot.Value<string>("Type");
                var fieldContent = fieldRoot.Value<string>("Value");
                var beginsGroup = fieldRoot.Value<string>("BeginsGroup");

                var fieldValue = ObjectSerializer.FromString(fieldTypeName, fieldContent);

                var allowedValues = fieldRoot.Value<TinyObject>("AllowedValues")?
                    .Select(p => new NamedValue { Name = p.Key, Value = ObjectSerializer.FromString(fieldTypeName, p.Value.Value<string>()) }).ToArray();

                effect.Config.UpdateField(fieldProperty.Key, fieldRoot.Value<string>("DisplayName"), null, fieldIndex++, fieldValue?.GetType(), fieldValue, allowedValues, beginsGroup);
            }

            var layersRoot = effectRoot.Value<TinyObject>("Layers");
            foreach (var layerProperty in layersRoot)
            {
                var layerEffect = effect;
                var layerGuid = layerProperty.Key;
                var layerRoot = layerProperty.Value;
                layerInserters[layerGuid] = () => layerEffect.AddPlaceholder(new(layerRoot.Value<string>("Name"), layerEffect)
                {
                    Guid = Guid.Parse(layerGuid),
                    OsbLayer = layerRoot.Value<OsbLayer>("OsbLayer"),
                    DiffSpecific = layerRoot.Value<bool>("DiffSpecific"),
                    Visible = layerRoot.Value<bool>("Visible")
                });
            }
        }

        if (effects.Count == 0) EffectsStatus = EffectStatus.Ready;

        var layersOrder = indexRoot.Values<string>("Layers").Distinct();
        if (layersOrder is not null) foreach (var layerGuid in layersOrder) if (layerInserters.TryGetValue(layerGuid, out var insertLayer)) insertLayer();

        // Insert all remaining layers
        foreach (var key in layersOrder is null ? layerInserters.Keys : layerInserters.Keys.Except(layersOrder))
        {
            var insertLayer = layerInserters[key];
            insertLayer();
        }
    }
    public static Project Create(string projectFolderName, string mapsetPath, bool withCommonScripts, ResourceContainer resourceContainer)
    {
        if (!Directory.Exists(ProjectsFolder)) Directory.CreateDirectory(ProjectsFolder);

        var hasInvalidCharacters = false;
        foreach (var character in Path.GetInvalidFileNameChars()) if (projectFolderName.Contains(character))
        {
            hasInvalidCharacters = true;
            break;
        }

        if (hasInvalidCharacters || string.IsNullOrWhiteSpace(projectFolderName)) throw new InvalidOperationException($"'{projectFolderName}' isn't a valid project folder name");

        var projectFolderPath = Path.Combine(ProjectsFolder, projectFolderName);
        if (Directory.Exists(projectFolderPath)) throw new InvalidOperationException($"A project already exists at '{projectFolderPath}'");

        Directory.CreateDirectory(projectFolderPath);
        Project project = new(Path.Combine(projectFolderPath, DefaultBinaryFilename), withCommonScripts, resourceContainer)
        {
            MapsetPath = mapsetPath
        };
        project.Save();

        return project;
    }

    public void ExportToOsb(bool exportOsb = true)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        string osuPath = null, osbPath = null;
        IEnumerable<EditorStoryboardLayer> localLayers = null, diffSpecific = null, sbLayer;
        Program.RunMainThread(() =>
        {
            if (osuPath != MainBeatmap.Path) osuPath = MainBeatmap.Path;
            if (osbPath != OsbPath) osbPath = OsbPath;

            if (!OwnsOsb && File.Exists(osbPath)) File.Move(osbPath, $"{osbPath}.bak");
            if (!OwnsOsb) OwnsOsb = true;

            localLayers = LayerManager.FindLayers(l => l.Visible);
            diffSpecific = LayerManager.FindLayers(l => l.DiffSpecific);
        });

        var usesOverlayLayer = localLayers.Any(l => l.OsbLayer == OsbLayer.Overlay);
        sbLayer = localLayers.Where(l => !l.DiffSpecific);

        if (!string.IsNullOrEmpty(osuPath) && diffSpecific.Any())
        {
            Trace.WriteLine($"Exporting diff specific events to {osuPath}");
            using SafeWriteStream stream = new(osuPath); using StreamWriter writer = new(stream, Encoding); using StreamReader reader = new(osuPath, Encoding);
            string line;
            var inEvents = false;
            var inStoryboard = false;

            while ((line = reader.ReadLine()) is not null)
            {
                var trimmedLine = line.Trim();
                if (!inEvents && trimmedLine == "[Events]") inEvents = true;
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

                                writer.WriteLine($"//Storyboard Layer {(int)osbLayer} ({osbLayer})");
                                foreach (var layer in diffSpecific) if (layer.OsbLayer == osbLayer && layer.Visible) layer.WriteOsb(writer, ExportSettings);
                            }
                            inStoryboard = true;
                        }
                    }
                    else if (inStoryboard && trimmedLine.StartsWith("//", StringComparison.Ordinal)) inStoryboard = false;

                    if (inStoryboard) continue;
                }
                writer.WriteLine(line);
            }
            stream.Commit();
        }

        if (exportOsb && sbLayer.Any())
        {
            Trace.WriteLine($"Exporting osb to {osbPath}");
            using StreamWriter writer = new(osbPath, false);
            writer.WriteLine("[Events]");
            writer.WriteLine("//Background and Video events");

            foreach (var osbLayer in OsbLayers)
            {
                if (osbLayer is OsbLayer.Overlay && !usesOverlayLayer) continue;

                writer.WriteLine($"//Storyboard Layer {(int)osbLayer} ({osbLayer})");
                foreach (var layer in sbLayer) if (layer.OsbLayer == osbLayer) layer.WriteOsb(writer, ExportSettings);
            }
            writer.WriteLine("//Storyboard Sound Samples");
        }
    }

    #endregion

    #region IDisposable Support

    public bool Disposed { get; private set; }
    public void Dispose()
    {
        if (!Disposed)
        {
            effectUpdateQueue.Dispose();
            assetWatcher.Dispose();
            MapsetManager?.Dispose();
            scriptManager.Dispose();
            TextureContainer.Dispose();
            AudioContainer.Dispose();

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true);

            effectUpdateQueue = null;
            assetWatcher = null;
            MapsetManager = null;
            scriptManager = null;
            TextureContainer = null;
            AudioContainer = null;

            Disposed = true;
        }
    }

    #endregion
}