namespace StorybrewEditor.Util;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BrewLib.Util;

public sealed class MultiFileWatcher : IDisposable
{
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

        watchedFilenames.Clear();
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

            if (!folderWatchers.TryGetValue(directoryPath, out var watcher))
            {
                folderWatchers[directoryPath] = watcher = new()
                {
                    Path = directoryPath,
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.DirectoryName
                };

                watcher.Created += watcher_Changed;
                watcher.Changed += watcher_Changed;
                watcher.Renamed += watcher_Changed;
                watcher.Error += (_, e) => Trace.TraceError($"Watcher error: {e.GetException()}");
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

                if (recursiveFolderWatchers.TryGetValue(parentDirectoryPath, out var watcher)) return;
                recursiveFolderWatchers[parentDirectoryPath] = watcher = new()
                {
                    Path = parentDirectoryPath,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.Size | NotifyFilters.DirectoryName
                };

                watcher.Created += watcher_Changed;
                watcher.Changed += watcher_Changed;
                watcher.Renamed += watcher_Changed;
                watcher.Error += (_, e) => Trace.TraceError($"Watcher error: {e.GetException()}");
                watcher.EnableRaisingEvents = true;

                Trace.WriteLine($"Watching folder and subfolders: {parentDirectoryPath}");
            }
            else
                Trace.TraceError($"Cannot watch file: {filename}, directory does not exist");
        }
    }

    void watcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"File {e.ChangeType.ToString().ToLowerInvariant()}: {e.FullPath}");
        scheduler.Schedule(e.FullPath, _ =>
        {
            if (disposed) return;

            lock (watchedFilenames)
                if (!watchedFilenames.Contains(e.FullPath))
                    return;

            Trace.WriteLine($"Watched file {e.ChangeType.ToString().ToLowerInvariant()}: {e.FullPath}");
            OnFileChanged?.Invoke(sender, e);
        });
    }
}