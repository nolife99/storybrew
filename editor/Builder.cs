namespace StorybrewEditor;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using BrewLib.Util;

public static class Builder
{
    static readonly string mainExecutablePath = "StorybrewEditor.exe";
    static readonly string[] ignoredPaths = [];

    public static void Build()
    {
        var archiveName =
            $"storybrew.{Program.Version.Major}.{Program.Version.Minor}-{RuntimeInformation.RuntimeIdentifier}.zip";

        var appDirectory = Path.GetDirectoryName(typeof(Editor).Assembly.Location);

        try
        {
            Trace.WriteLine($"\n\nBuilding {archiveName}\n");

            var scriptsDirectory = Path.GetFullPath(Path.Combine(appDirectory, "../../../../../scripts"));

            using FileStream stream = new(archiveName, FileMode.Create, FileAccess.ReadWrite);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);
            addFile(archive, mainExecutablePath, appDirectory);
            addFile(archive, "StorybrewEditor.runtimeconfig.json", appDirectory);

            foreach (var path in Directory.EnumerateFiles(appDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                addFile(archive, path, appDirectory);

            foreach (var path in Directory.EnumerateFiles(appDirectory, "*.xml", SearchOption.TopDirectoryOnly))
                addFile(archive, path, appDirectory);

            foreach (var path in Directory.EnumerateFiles(scriptsDirectory, "*.cs", SearchOption.TopDirectoryOnly))
                addFile(archive, path, scriptsDirectory, "scripts");

            var nativeDllDir = Path.Combine("runtimes", RuntimeInformation.RuntimeIdentifier, "native");
            foreach (var path in Directory.EnumerateFiles(nativeDllDir, "*.*", SearchOption.TopDirectoryOnly))
                addFile(archive, Path.GetFileName(path), nativeDllDir);

            PathHelper.OpenExplorer(appDirectory);
        }
        catch (Exception e)
        {
            Environment.FailFast("Build failed", e);
        }
    }

    /* static void testUpdate(string archiveName)
    {
        var previousVersion = $"{Program.Version.Major}.{Program.Version.Minor - 1}";
        var previousArchiveName = $"storybrew.{previousVersion}.zip";
        if (!File.Exists(previousArchiveName)) using (var webClient = new WebClient())
        {
            webClient.Headers.Add("user-agent", Program.Name);
            webClient.DownloadFile($"https://github.com/{Program.Repository}/releases/download/{previousVersion}/{previousArchiveName}", previousArchiveName);
        }

        var updateTestPath = Path.GetFullPath("updatetest");
        var updateFolderPath = Path.GetFullPath(Path.Combine(updateTestPath, Updater.UpdateFolderPath));
        var executablePath = Path.GetFullPath(Path.Combine(updateFolderPath, mainExecutablePath));

        if (Directory.Exists(updateTestPath))
        {
            foreach (var filename in Directory.GetFiles(updateTestPath, "*", SearchOption.AllDirectories))
                File.SetAttributes(filename, FileAttributes.Normal);

            Directory.Delete(updateTestPath, true);
        }
        Directory.CreateDirectory(updateTestPath);

        ZipFile.ExtractToDirectory(previousArchiveName, updateTestPath);
        ZipFile.ExtractToDirectory(archiveName, updateFolderPath);

        Process.Start(new ProcessStartInfo(executablePath, $"update \"{updateTestPath}\" {previousVersion}")
        {
            WorkingDirectory = updateFolderPath,
        });
    } */
    static void addFile(ZipArchive archive, string path, string sourceDirectory, string targetPath = null)
    {
        path = Path.Combine(sourceDirectory, path);

        var entryName = Path.GetRelativePath(sourceDirectory, path);
        if (targetPath is not null)
        {
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
            entryName = Path.Combine(targetPath, entryName);
        }

        if (ignoredPaths.Contains(entryName)) return;

        if (entryName != mainExecutablePath && Path.GetExtension(entryName) == ".exe") entryName += "_";

        Trace.WriteLine($"Adding {entryName} to archive");
        archive.CreateEntryFromFile(path, entryName, CompressionLevel.SmallestSize);
    }
}