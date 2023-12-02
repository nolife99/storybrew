using StorybrewEditor.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace StorybrewEditor.Mapset;

public sealed class MapsetManager : IDisposable
{
    readonly string path;
    readonly bool logLoadingExceptions;

    readonly HashSet<EditorBeatmap> beatmaps = [];
    public IEnumerable<EditorBeatmap> Beatmaps => beatmaps;
    public int BeatmapCount => beatmaps.Count;

    public MapsetManager(string path, bool logLoadingExceptions)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Mapset path cannot be empty", nameof(path));

        this.path = path;
        this.logLoadingExceptions = logLoadingExceptions;

        loadBeatmaps();
        initializeMapsetWatcher();
    }

    #region Beatmaps

    void loadBeatmaps()
    {
        if (!Directory.Exists(path)) return;

        var maps = Directory.GetFiles(path, "*.osu", SearchOption.TopDirectoryOnly);
        Array.Sort(maps);

        foreach (var beatmapPath in maps) try
        {
            beatmaps.Add(EditorBeatmap.Load(beatmapPath));
        }
        catch (Exception e)
        {
            if (logLoadingExceptions) Trace.WriteLine($"Failed to load beatmap: {e}");
            else throw;
        }
    }

    #endregion

    #region Events

    FileSystemWatcher fileWatcher;
    readonly ThrottledActionScheduler scheduler = new();

    public event FileSystemEventHandler OnFileChanged;

    void initializeMapsetWatcher()
    {
        if (!Directory.Exists(path)) return;

        fileWatcher = new()
        {
            Path = path,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.Size
        };

        fileWatcher.Created += mapsetFileWatcher_Changed;
        fileWatcher.Changed += mapsetFileWatcher_Changed;
        fileWatcher.Renamed += mapsetFileWatcher_Changed;
        fileWatcher.Error += (sender, e) => Trace.WriteLine($"Watcher error (mapset): {e.GetException()}");
        fileWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (mapset): {path}");
    }
    void mapsetFileWatcher_Changed(object sender, FileSystemEventArgs e) => scheduler.Schedule(e.FullPath, key =>
    {
        if (Path.GetExtension(e.Name) == ".osu") Trace.WriteLine($"Watched mapset file {e.ChangeType.ToString().ToLowerInvariant()}: {e.FullPath}");
        OnFileChanged?.Invoke(sender, e);
    });

    #endregion

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            fileWatcher?.Dispose();
            beatmaps.Clear();
            fileWatcher = null;
            disposed = true;
        }
    }
}