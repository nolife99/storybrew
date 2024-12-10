namespace BrewLib.Data;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using Util;

public class AssemblyResourceContainer(Assembly assembly = null, string baseNamespace = null, string basePath = null)
    : ResourceContainer
{
    static readonly Dictionary<string, ZipArchive> archives = [];
    readonly Assembly assembly = assembly ?? Assembly.GetEntryAssembly();

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
                var stream = assembly.GetManifestResourceStream($"{baseNamespace}.zip");
                if (stream is not null)
                {
                    using ZipArchive archive = new(stream, ZipArchiveMode.Read, false);

                    var entry = archive.GetEntry(path);
                    if (entry is not null)
                    {
                        using var entryStream = entry.Open();
                        SafeUnmanagedMemoryStream bytes = new();
                        entryStream.CopyTo(bytes);

                        bytes.Position = 0;
                        return bytes;
                    }
                }
                else
                {
                    stream = assembly.GetManifestResourceStream($"{baseNamespace}.{path.Replace('\\', '.').Replace('/', '.')}");
                    if (stream is not null) return stream;
                }
            }
        }

        Trace.TraceWarning($"Not found: {path} ({sources})", "Resources");
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
    {
        if (Path.IsPathRooted(path)) throw new ArgumentException("Resource paths must be relative", path);
        return new(basePath is not null ? Path.Combine(basePath, path) : path);
    }
}