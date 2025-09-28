namespace BrewLib.IO;

using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using BrewLib.Util;
using SDL3;

public sealed class AssemblyResourceContainer(Assembly assembly, string baseNamespace = null, string basePath = null)
    : ResourceContainer
{
    readonly string baseNamespace = baseNamespace ?? $"{assembly.EntryPoint.DeclaringType.Namespace}.Resources",
        basePath = basePath ?? "resources";

    public Stream GetStream(string path, ResourceSource sources)
    {
        if (path is null) return null;

        if (Path.IsPathRooted(path))
        {
            if ((sources & ResourceSource.Absolute) != 0)
            {
                if (File.Exists(path)) return File.OpenRead(path);
            }
            else throw new InvalidOperationException($"Resource paths must be relative ({path})");
        }
        else
        {
            if ((sources & ResourceSource.Relative) != 0)
            {
                var combinedPath = basePath is not null ? Path.Combine(basePath, path) : path;
                if (File.Exists(combinedPath)) return File.OpenRead(combinedPath);
            }

            if ((sources & ResourceSource.Embedded) != 0)
            {
                var stream = assembly.GetManifestResourceStream(
                    $"{baseNamespace}.{path.Replace('\\', '.').Replace('/', '.')}.zz");

                if (stream is not null) return new DeflateStream(stream, CompressionMode.Decompress);
            }
        }

        SDL.LogWarn(SDL.LogCategory.Application, $"Not found: {path} ({sources})");
        return null;
    }

    public string GetString(string path, ResourceSource sources = ResourceSource.Embedded)
    {
        var resource = GetStream(path, sources);
        if (resource is null) return null;

        using StreamReader stream = new(resource, Encoding.UTF8, leaveOpen: false);
        return stream.ReadToEnd().StripUtf8Bom();
    }

    public SafeWriteStream GetWriteStream(string path)
        => Path.IsPathRooted(path) ?
            throw new ArgumentException("Resource paths must be relative", path) :
            new(basePath is not null ? Path.Combine(basePath, path) : path);
}