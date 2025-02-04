﻿namespace BrewLib.IO;

using System;
using System.Collections.Generic;
using System.IO;

public sealed class SafeDirectoryWriter : IDisposable
{
    readonly HashSet<string> paths = [];
    readonly string targetDirectory, tempDirectory, backupDirectory;
    bool committed;

    public SafeDirectoryWriter(string targetDirectory)
    {
        this.targetDirectory = targetDirectory;
        tempDirectory = targetDirectory + ".tmp";
        backupDirectory = targetDirectory + ".bak";

        if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        Directory.CreateDirectory(tempDirectory);
    }

    public void Dispose()
    {
        if (committed)
        {
            if (Directory.Exists(targetDirectory))
            {
                if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
                Directory.Move(targetDirectory, backupDirectory);
            }

            Directory.Move(tempDirectory, targetDirectory);
            if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true);
        }
        else if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
    }

    public string GetPath(string path)
    {
        var fullPath = Path.Combine(tempDirectory, path);
        paths.Add(fullPath);
        return fullPath;
    }

    public void Commit(bool checkPaths = true)
    {
        if (checkPaths)
        {
            if (paths.Count == 0) throw new InvalidOperationException("No file path requested");

            foreach (var path in paths)
            {
                FileInfo file = new(path);

                if (!file.Exists) throw new InvalidOperationException($"File path requested but not created: {path}");
                if (file.Length == 0) throw new InvalidOperationException($"File path requested but is empty: {path}");
            }
        }

        committed = true;
    }
}