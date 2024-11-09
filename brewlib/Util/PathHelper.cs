namespace BrewLib.Util;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

public static class PathHelper
{
    const char StandardDirectorySeparator = '/';

    static readonly HashSet<char> invalidChars =
    [
        '"',
        '<',
        '>',
        '|',
        '\0',
        '\u0001',
        '\u0002',
        '\u0003',
        '\u0004',
        '\u0005',
        '\u0006',
        '\a',
        '\b',
        '\t',
        '\n',
        '\v',
        '\f',
        '\r',
        '\u000e',
        '\u000f',
        '\u0010',
        '\u0011',
        '\u0012',
        '\u0013',
        '\u0014',
        '\u0015',
        '\u0016',
        '\u0017',
        '\u0018',
        '\u0019',
        '\u001a',
        '\u001b',
        '\u001c',
        '\u001d',
        '\u001e',
        '\u001f'
    ];

    public static void OpenExplorer(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public static void SafeDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (SystemException) { }
    }
    public static string WithStandardSeparators(string path)
    {
        var chars = new Span<char>(path.ToCharArray());
        if (Path.DirectorySeparatorChar != StandardDirectorySeparator)
            chars.Replace(Path.DirectorySeparatorChar, StandardDirectorySeparator);

        chars.Replace('\\', StandardDirectorySeparator);
        return chars.ToString();
    }
    public static void WithStandardSeparatorsUnsafe(ReadOnlySpan<char> path)
    {
        var chars = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(path), path.Length);
        if (Path.DirectorySeparatorChar != StandardDirectorySeparator)
            chars.Replace(Path.DirectorySeparatorChar, StandardDirectorySeparator);

        chars.Replace('\\', StandardDirectorySeparator);
    }
    public static bool FolderContainsPath(string folder, string path)
    {
        folder = WithStandardSeparators(Path.GetFullPath(folder));
        path = WithStandardSeparators(Path.GetFullPath(path));

        var _folder = folder.AsSpan().TrimEnd('/');
        var _path = path.AsSpan().TrimEnd('/');

        return _path.Length >= _folder.Length + 1 && _path[_folder.Length] == '/' &&
            _path.StartsWith(_folder, StringComparison.Ordinal);
    }
    public static string GetRelativePath(string folder, string path) => Path.GetRelativePath(folder, path);

    public static bool IsValidPath(string path) => path.All(c => !invalidChars.Contains(c));
    public static bool IsValidFilename(string filename) => filename.All(character
        => !invalidChars.Contains(character) &&
        (char.IsLetter(character) && (char.IsLower(character) || char.IsUpper(character)) || char.IsDigit(character)));
}