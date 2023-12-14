using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BrewLib.Util;

namespace StorybrewEditor.Util;

public sealed class MultiFileWatcher : IDisposable
{
    readonly Dictionary<string, FileSystemWatcher> folderWatchers = [], recursiveFolderWatchers = [];
    HashSet<string> watchedFilenames = [];
    readonly ThrottledActionScheduler scheduler = new();

    public IEnumerable<string> WatchedFilenames => watchedFilenames;

    public event FileSystemEventHandler OnFileChanged;

    public void Watch(IEnumerable<string> filenames)
    {
        foreach (var filename in filenames) Watch(filename);
    }
    public void Watch(string filename)
    {
        filename = Path.GetFullPath(filename);
        var directoryPath = Path.GetDirectoryName(filename);

        lock (watchedFilenames)
        {
            if (watchedFilenames.Contains(filename)) return;
            watchedFilenames.Add(filename);
        }

        if (Directory.Exists(directoryPath))
        {
            // The folder containing the file to watch exists, 
            // only watch that folder

            if (!folderWatchers.TryGetValue(directoryPath, out FileSystemWatcher watcher))
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
                watcher.Error += (sender, e) => Trace.WriteLine($"Watcher error: {e.GetException()}");
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
            while (parentDirectory is not null && !parentDirectory.Exists) parentDirectory = Directory.GetParent(parentDirectory.FullName);

            if (parentDirectory is not null && parentDirectory != parentDirectory.Root)
            {
                var parentDirectoryPath = parentDirectory.ToString();

                if (!recursiveFolderWatchers.TryGetValue(parentDirectoryPath, out var watcher))
                {
                    recursiveFolderWatchers[parentDirectoryPath] = watcher = new()
                    {
                        Path = parentDirectoryPath,
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.Size | NotifyFilters.DirectoryName
                    };

                    watcher.Created += watcher_Changed;
                    watcher.Changed += watcher_Changed;
                    watcher.Renamed += watcher_Changed;
                    watcher.Error += (sender, e) => Trace.WriteLine($"Watcher error: {e.GetException()}");
                    watcher.EnableRaisingEvents = true;

                    Trace.WriteLine($"Watching folder and subfolders: {parentDirectoryPath}");
                }
            }
            else Trace.WriteLine($"Cannot watch file: {filename}, directory does not exist");
        }
    }
    void watcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"File {e.ChangeType.ToString().ToLowerInvariant()}: {e.FullPath}");
        scheduler.Schedule(e.FullPath, key =>
        {
            if (disposed) return;

            lock (watchedFilenames) if (!watchedFilenames.Contains(e.FullPath)) return;

            Trace.WriteLine($"Watched file {e.ChangeType.ToString().ToLowerInvariant()}: {e.FullPath}");
            OnFileChanged?.Invoke(sender, e);
        });
    }

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            folderWatchers.Dispose();
            recursiveFolderWatchers.Dispose();

            lock (watchedFilenames) watchedFilenames.Clear();

            watchedFilenames = null;
            OnFileChanged = null;
            disposed = true;
        }
    }
}