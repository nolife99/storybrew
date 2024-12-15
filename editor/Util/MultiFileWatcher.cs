namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BrewLib.Util;

public sealed class MultiFileWatcher : IDisposable
{
    static readonly Lock fileLock = new();
    readonly Dictionary<string, FileSystemWatcher> folderWatchers = [], recursiveFolderWatchers = [];
    readonly ThrottledActionScheduler scheduler = new();

    bool disposed;
    HashSet<string> watchedFilenames = [];

    public IEnumerable<string> WatchedFilenames => watchedFilenames;

    public void Dispose()
    {
        if (disposed) return;

        folderWatchers.Dispose();
        recursiveFolderWatchers.Dispose();

        watchedFilenames = null;

        OnFileChanged = null;
        disposed = true;
    }

    public event FileSystemEventHandler OnFileChanged;

    public void Watch(IEnumerable<string> filenames)
    {
        foreach (var filename in filenames) Watch(filename);
    }

    public void Watch(string filename)
    {
        filename = Path.GetFullPath(filename);
        var directoryPath = Path.GetDirectoryName(filename);

        watchedFilenames.Add(filename);
        if (Directory.Exists(directoryPath))
        {
            // The folder containing the file to watch exists,
            // only watch that folder

            ref var watcher = ref CollectionsMarshal.GetValueRefOrAddDefault(folderWatchers, directoryPath, out var exists);
            if (!exists)
            {
                watcher = new()
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

                Trace.WriteLine($"Watching folder: {directoryPath}");
            }

            Trace.WriteLine($"Watching file: {filename}");
        }
        else
        {
            // The folder containing the file to watch does not exist,
            // find a parent to watch subfolders from

            var parentDirectory = Directory.GetParent(directoryPath);
            while (parentDirectory is not null && !parentDirectory.Exists)
                parentDirectory = Directory.GetParent(parentDirectory.FullName);

            if (parentDirectory is not null && parentDirectory != parentDirectory.Root)
            {
                var parentDirectoryPath = parentDirectory.ToString();

                ref var watcher =
                    ref CollectionsMarshal.GetValueRefOrAddDefault(recursiveFolderWatchers, parentDirectoryPath, out var exists);

                if (exists) return;
                watcher = new()
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

                Trace.WriteLine($"Watching folder and subfolders: {parentDirectoryPath}");
            }
            else Trace.TraceError($"Watching file: {filename}, directory does not exist");
        }
    }

    void watcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"File {e.ChangeType}: {e.FullPath}");
        scheduler.Schedule(e.FullPath,
            _ =>
            {
                if (disposed) return;

                lock (fileLock)
                    if (!watchedFilenames.Contains(e.FullPath))
                        return;

                Trace.WriteLine($"Watched file {e.ChangeType}: {e.FullPath}");
                OnFileChanged?.Invoke(sender, e);
            });
    }
}