using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BrewLib.Util
{
    public static class PathHelper
    {
        public static void OpenExplorer(string path) => Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });

        public static string WithPlatformSeparators(string path)
        {
            if (Path.DirectorySeparatorChar != '/') path = path.Replace('/', Path.DirectorySeparatorChar);
            if (Path.DirectorySeparatorChar != '\\') path = path.Replace('\\', Path.DirectorySeparatorChar);
            return path;
        }

        public const char StandardDirectorySeparator = '/';
        public static string WithStandardSeparators(string path)
        {
            if (Path.DirectorySeparatorChar != StandardDirectorySeparator)
                path = path.Replace(Path.DirectorySeparatorChar, StandardDirectorySeparator);

            path = path.Replace('\\', StandardDirectorySeparator);
            return path;
        }
        public static bool FolderContainsPath(string folder, string path)
        {
            folder = WithStandardSeparators(Path.GetFullPath(folder)).TrimEnd('/');
            path = WithStandardSeparators(Path.GetFullPath(path)).TrimEnd('/');

            return path.Length >= folder.Length + 1 && path[folder.Length] == '/' && path.StartsWith(folder, StringComparison.Ordinal);
        }
        public static string GetRelativePath(string folder, string path)
        {
            folder = WithStandardSeparators(Path.GetFullPath(folder)).TrimEnd('/');
            path = WithStandardSeparators(Path.GetFullPath(path)).TrimEnd('/');

            if (path.Length < folder.Length + 1 || path[folder.Length] != '/' || !path.StartsWith(folder, StringComparison.Ordinal))
                throw new ArgumentException(path + " isn't contained in " + folder);

            return path.Substring(folder.Length + 1);
        }

        static readonly HashSet<char> invalidChars = new()
        {
            '"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005',
            '\u0006', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '\u000e', '\u000f',
            '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019',
            '\u001a', '\u001b', '\u001c', '\u001d', '\u001e', '\u001f'
        };
        public static bool IsValidPath(string path)
        {
            foreach (var character in path) if (invalidChars.Contains(character)) return false;
            return true;
        }
        public static bool IsValidFilename(string filename)
        {
            foreach (var character in filename) if (invalidChars.Contains(character) ||
                !(char.IsLetter(character) && (char.IsLower(character) || char.IsUpper(character)) || char.IsDigit(character))) return false;
            return true;
        }
    }
}