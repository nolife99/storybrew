using System.IO;

namespace BrewLib.Util;

public class SafeDirectoryReader
{
    public readonly string Path;

    public SafeDirectoryReader(string targetDirectory)
    {
        var backupDirectory = targetDirectory + ".bak";
        Path = Directory.Exists(targetDirectory) || !Directory.Exists(backupDirectory) ? targetDirectory : backupDirectory;
    }

    public string GetPath(string path) => System.IO.Path.Combine(Path, path);
}