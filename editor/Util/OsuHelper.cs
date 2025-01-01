namespace StorybrewEditor.Util;

using System;
using System.IO;
using Microsoft.Win32;

public static class OsuHelper
{
    public static string GetOsuExePath()
    {
        try
        {
            using var registryKey = Registry.ClassesRoot.OpenSubKey("osu\\DefaultIcon");

            if (registryKey is not null)
            {
                var value = registryKey.GetValue(null).ToString();
                var startIndex = value.IndexOf('"');
                var endIndex = value.LastIndexOf('"');
                return value.Substring(startIndex + 1, endIndex - 1);
            }
        }
        catch
        {
            // ignored
        }

        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "osu!",
            "osu!.exe");

        return File.Exists(defaultPath) ? defaultPath : "";
    }

    public static string GetOsuSongFolder()
    {
        var osuPath = GetOsuExePath();
        if (string.IsNullOrEmpty(osuPath)) return Path.GetPathRoot(Environment.CurrentDirectory);

        var osuFolder = Path.GetDirectoryName(osuPath);
        var songsFolder = Path.Combine(osuFolder, "Songs");
        return Directory.Exists(songsFolder) ? songsFolder : osuFolder;
    }
}