namespace StorybrewEditor;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using BrewLib.Util;
using Util;

public static class Updater
{
    public const string UpdateArchivePath = "cache/net/update", UpdateFolderPath = "cache/update", FirstRunPath = "firstrun";

    static readonly string[] ignoredPaths = [".vscode/", "cache/", "logs/", "settings.cfg"], readOnlyPaths = ["scripts/"];

    static readonly Version readOnlyVersion = new(1, 8);

    public static void OpenLatestReleasePage() => NetHelper.OpenUrl($"https://github.com/{Program.Repository}/releases/latest");

    public static void Update(string destinationFolder, Version fromVersion)
    {
        Trace.WriteLine($"Updating from version {fromVersion} to {Program.Version}");
        var updaterPath = typeof(Editor).Assembly.Location;
        var sourceFolder = Path.GetDirectoryName(updaterPath);
        try
        {
            replaceFiles(sourceFolder, destinationFolder, fromVersion);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Replacing files: {e}");
            MessageBox.Show($"Update failed, please update manually.\n\n{e}", Program.FullName);
            OpenLatestReleasePage();
            Program.Report("updatefail", e);
            return;
        }

        try
        {
            updateData(destinationFolder, fromVersion);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Updating data: {e}");
            MessageBox.Show($"Failed to update data.\n\n{e}", Program.FullName);
            Program.Report("updatefail", e);
        }

        var relativeProcessPath = PathHelper.GetRelativePath(sourceFolder, updaterPath);
        var processPath = Path.Combine(destinationFolder, relativeProcessPath);

        Trace.WriteLine($"\nUpdate complete, starting {processPath}");
        Process.Start(new ProcessStartInfo(processPath) { UseShellExecute = true, WorkingDirectory = destinationFolder })
            .Dispose();
    }

    public static void NotifyEditorRun()
    {
        if (File.Exists(FirstRunPath))
        {
            File.Delete(FirstRunPath);
            firstRun();
        }

        if (File.Exists(UpdateArchivePath)) Misc.WithRetries(() => File.Delete(UpdateArchivePath), canThrow: false);
        if (Directory.Exists(UpdateFolderPath)) Misc.WithRetries(() => Directory.Delete(UpdateFolderPath, true), canThrow: false);
    }

    static void updateData(string destinationFolder, Version fromVersion)
    {
        Settings settings = new(Path.Combine(destinationFolder, Settings.DefaultPath));
        if (fromVersion < new Version(1, 70)) settings.Volume.Set(Math.Pow(settings.Volume, .25));
        settings.Save();

        if (fromVersion < new Version(1, 57))
        {
            var dllPath = Path.Combine(destinationFolder, "ManagedBass.PInvoke.dll");
            if (File.Exists(dllPath))
            {
                Trace.WriteLine($"Removing {dllPath}");
                Misc.WithRetries(() => File.Delete(dllPath), canThrow: false);
            }
        }

        if (fromVersion >= new Version(1, 65)) return;

        var oldRoslynFolder = Path.Combine(destinationFolder, "bin");
        if (!Directory.Exists(oldRoslynFolder)) return;

        Trace.WriteLine($"Removing {oldRoslynFolder}");
        Misc.WithRetries(() => Directory.Delete(oldRoslynFolder, true), canThrow: false);
    }

    static void firstRun()
    {
        Trace.WriteLine("First run\n");

        foreach (var exeFilename in Directory.EnumerateFiles(Path.GetDirectoryName(typeof(Editor).Assembly.Location), "*.exe_",
            SearchOption.AllDirectories))
        {
            var newFilename = Path.ChangeExtension(exeFilename, ".exe");
            Trace.WriteLine($"Renaming {exeFilename} to {newFilename}");
            Misc.WithRetries(() => File.Move(exeFilename, newFilename), canThrow: false);
        }

        foreach (var scriptFilename in Directory.EnumerateFiles("scripts", "*.cs", SearchOption.TopDirectoryOnly))
            File.SetAttributes(scriptFilename, FileAttributes.ReadOnly);
    }

    static void replaceFiles(string sourceFolder, string destinationFolder, Version fromVersion)
    {
        Trace.WriteLine($"\nCopying files from {sourceFolder} to {destinationFolder}");
        foreach (var sourceFilename in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var relativeFilename = PathHelper.GetRelativePath(sourceFolder, sourceFilename);

            if (matchFilter(relativeFilename, ignoredPaths))
            {
                Trace.WriteLine($"  Ignoring {relativeFilename}");
                continue;
            }

            var readOnly = matchFilter(relativeFilename, readOnlyPaths);

            var destinationFilename = Path.Combine(destinationFolder, relativeFilename);
            if (Path.GetExtension(destinationFilename) == ".exe_")
                destinationFilename = Path.ChangeExtension(destinationFilename, ".exe");

            Trace.WriteLine($"  Copying {relativeFilename} to {destinationFilename}");
            replaceFile(sourceFilename, destinationFilename, readOnly, fromVersion);
        }
    }

    static void replaceFile(string sourceFilename, string destinationFilename, bool readOnly, Version fromVersion)
    {
        var destinationFolder = Path.GetDirectoryName(destinationFilename);
        if (!Directory.Exists(destinationFolder)) Directory.CreateDirectory(destinationFolder);

        if (readOnly && File.Exists(destinationFilename))
        {
            var attributes = File.GetAttributes(destinationFilename);
            if ((attributes & FileAttributes.ReadOnly) == 0)
            {
                // Don't update files that became readonly when coming from a version that didn't have them
                if (fromVersion < readOnlyVersion) return;

                Trace.WriteLine($"  Creating backup for {destinationFilename}");
                var backupFilename = destinationFilename + $".{DateTimeOffset.UtcNow.Ticks}.bak";
                File.Move(destinationFilename, backupFilename);
            }
            else File.SetAttributes(destinationFilename, attributes & ~FileAttributes.ReadOnly);
        }

        Misc.WithRetries(() => File.Copy(sourceFilename, destinationFilename, true), 5000);
        if (readOnly) File.SetAttributes(destinationFilename, FileAttributes.ReadOnly);
    }

    static bool matchFilter(string filename, string[] filters)
        => filters.Any(filter => filename.StartsWith(filter, StringComparison.Ordinal));
}