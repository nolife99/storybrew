namespace StorybrewEditor.Util;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Tiny.PooledCollections.Generic;

public sealed class MultiFileWatcher : IDisposable, IEnumerable<string>
{
    static readonly Lock fileLock = new();
    readonly PooledDictionary<string, FileSystemWatcher> folderWatchers = new(), recursiveFolderWatchers = new();
    readonly ThrottledActionScheduler scheduler = new();
    readonly PooledHashSet<string> watchedFilenames = [];

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var watcher in folderWatchers.Values) watcher.Dispose();
        folderWatchers.Dispose();

        foreach (var watcher in recursiveFolderWatchers.Values) watcher.Dispose();
        recursiveFolderWatchers.Dispose();

        watchedFilenames.Dispose();

        OnFileChanged = null;
        disposed = true;
    }

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => watchedFilenames.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public event FileSystemEventHandler OnFileChanged;

    public void Watch(IEnumerable<string> filenames)
    {
        if (filenames is MultiFileWatcher hs)
            foreach (var filename in hs)
                Watch(filename);
        else
            foreach (var filename in filenames)
                Watch(filename);
    }

    public void Watch(string filename) => watchInternal(Path.GetFullPath(filename));

    void watchInternal(string filename)
    {
        var directoryPath = Path.GetDirectoryName(filename);

        watchedFilenames.Add(filename);
        if (Directory.Exists(directoryPath))
        {
            if (folderWatchers.ContainsKey(directoryPath)) return;

            var watcher = folderWatchers[directoryPath] = new()
            {
                Path = directoryPath,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.Size | NotifyFilters.DirectoryName
            };

            watcher.Created += watcher_Changed;
            watcher.Changed += watcher_Changed;
            watcher.Renamed += watcher_Changed;
            watcher.Error += (_, e) => Trace.TraceError($"Watcher: {e.GetException()}");
            watcher.EnableRaisingEvents = true;
        }
        else
        {
            var parentDirectory = Directory.GetParent(directoryPath);
            while (parentDirectory is not null && !parentDirectory.Exists)
                parentDirectory = Directory.GetParent(parentDirectory.FullName);

            if (parentDirectory is null || parentDirectory == parentDirectory.Root) return;

            var parentDirectoryPath = parentDirectory.ToString();
            if (recursiveFolderWatchers.ContainsKey(parentDirectoryPath)) return;

            var watcher = recursiveFolderWatchers[parentDirectoryPath] = new()
            {
                Path = parentDirectoryPath,
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.Size | NotifyFilters.DirectoryName
            };

            watcher.Created += watcher_Changed;
            watcher.Changed += watcher_Changed;
            watcher.Renamed += watcher_Changed;
            watcher.Error += (_, e) => Trace.TraceError($"Watcher: {e.GetException()}");
            watcher.EnableRaisingEvents = true;
        }
    }

    void watcher_Changed(object sender, FileSystemEventArgs e)
        => scheduler.Schedule(e.FullPath,
            _ =>
            {
                if (disposed) return;

                lock (fileLock)
                    if (!watchedFilenames.Contains(e.FullPath))
                        return;

                Trace.WriteLine($"Watched file {e.ChangeType}: {e.FullPath}");
                OnFileChanged?.Invoke(sender, e);
            });

    public PooledHashSet<string>.Enumerator GetEnumerator() => watchedFilenames.GetEnumerator();
}