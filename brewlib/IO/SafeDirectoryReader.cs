namespace BrewLib.IO;

using System.IO;

public class SafeDirectoryReader
{
    public SafeDirectoryReader(string targetDirectory)
    {
        var backupDirectory = targetDirectory + ".bak";
        Path = Directory.Exists(targetDirectory) || !Directory.Exists(backupDirectory) ? targetDirectory : backupDirectory;
    }
    public string Path { get; }
    public string GetPath(string path) => System.IO.Path.Combine(Path, path);
}