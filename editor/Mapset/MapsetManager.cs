namespace StorybrewEditor.Mapset;

using System;
using System.Diagnostics;
using System.IO;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.Temporary;

public sealed class MapsetManager : IDisposable
{
    readonly PooledList<EditorBeatmap> beatmaps = new();
    readonly bool logLoadingExceptions;
    readonly string path;

    bool disposed;

    public MapsetManager(string path, bool logLoadingExceptions)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Mapset path cannot be empty", nameof(path));

        this.path = path;
        this.logLoadingExceptions = logLoadingExceptions;

        loadBeatmaps();
        initializeMapsetWatcher();
    }

    public ReadOnlySpan<EditorBeatmap> Beatmaps => beatmaps.AsReadOnlySpan();
    public int BeatmapCount => beatmaps.Count;
    public void Dispose() => Dispose(true);

    #region Beatmaps

    void loadBeatmaps()
    {
        if (!Directory.Exists(path)) return;

        using var maps = TempList.Create(Directory.EnumerateFiles(path, "*.osu", SearchOption.TopDirectoryOnly));

        maps.Sort();
        foreach (var beatmapPath in maps)
            try
            {
                beatmaps.Add(EditorBeatmap.Load(beatmapPath));
            }
            catch (Exception e)
            {
                if (logLoadingExceptions) Trace.TraceError($"Loading beatmap: {e}");
                else throw;
            }
    }

    #endregion

    void Dispose(bool disposing)
    {
        if (disposed) return;

        fileWatcher?.Dispose();

        if (!disposing) return;

        beatmaps.Dispose();

        fileWatcher = null;
        disposed = true;
    }

    #region Events

    FileSystemWatcher fileWatcher;
    readonly ThrottledActionScheduler scheduler = new();

    public event FileSystemEventHandler OnFileChanged;

    void initializeMapsetWatcher()
    {
        if (!Directory.Exists(path)) return;

        fileWatcher = new() { Path = path, IncludeSubdirectories = true, NotifyFilter = NotifyFilters.Size };

        fileWatcher.Created += mapsetFileWatcher_Changed;
        fileWatcher.Changed += mapsetFileWatcher_Changed;
        fileWatcher.Renamed += mapsetFileWatcher_Changed;
        fileWatcher.Error += (_, e) => Trace.TraceError($"Watcher (mapset): {e.GetException()}");

        fileWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (mapset): {path}");
    }

    void mapsetFileWatcher_Changed(object sender, FileSystemEventArgs e)
        => scheduler.Schedule(e.FullPath,
            _ =>
            {
                if (Path.GetExtension(e.Name) == ".osu")
                    Trace.WriteLine($"Watched mapset file {e.ChangeType}: {e.FullPath}");

                OnFileChanged?.Invoke(sender, e);
            });

    #endregion
}