namespace BrewLib.Util;

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SDL3;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public static class PathHelper
{
    const char StandardDirectorySeparator = '/';

    static readonly SearchValues<char> invalidChars = SearchValues.Create('"',
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
        '\u001f');

    public static void OpenExplorer(scoped ReadOnlySpan<char> path)
    {
        var temp = ValueList.Create("file:///");
        temp.AddRange(path);

        ThreadPool.UnsafeQueueUserWorkItem(s =>
            {
                SDL.OpenURL(s.AsReadOnlySpan());
                s.Dispose();
            },
            temp,
            false);
    }

    public static void SafeDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
    }

    public static string WithStandardSeparators(string path)
    {
        Span<char> chars = stackalloc char[path.Length];
        path.CopyTo(chars);

        if (Path.DirectorySeparatorChar != StandardDirectorySeparator)
            chars.Replace(Path.DirectorySeparatorChar, StandardDirectorySeparator);

        chars.Replace('\\', StandardDirectorySeparator);
        return chars.SequenceEqual(path) ? path : chars.ToString();
    }

    public static void WithStandardSeparatorsUnsafe(scoped ReadOnlySpan<char> path)
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
            _path.StartsWith(_folder, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetRelativePath(string folder, string path) => Path.GetRelativePath(folder, path);

    public static bool IsValidPath(scoped ReadOnlySpan<char> path) => !path.ContainsAny(invalidChars);

    public static bool IsValidFilename(char character)
        => !invalidChars.Contains(character) &&
            (char.IsLetter(character) && (char.IsLower(character) || char.IsUpper(character)) ||
                char.IsDigit(character));
}