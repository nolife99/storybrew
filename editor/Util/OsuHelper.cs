using System;
using System.IO;
using Microsoft.Win32;

namespace StorybrewEditor.Util;

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
        catch { }

        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!", "osu!.exe");
        if (File.Exists(defaultPath)) return defaultPath;
        return "";
    }

    public static string GetOsuFolder()
    {
        var osuPath = GetOsuExePath();
        if (string.IsNullOrEmpty(osuPath)) return Path.GetPathRoot(Environment.CurrentDirectory);
        return Path.GetDirectoryName(osuPath);
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