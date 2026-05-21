namespace BrewLib.IO;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using SDL3;
using Util;

public sealed class AssemblyResourceContainer(Assembly assembly, string baseNamespace = null, string basePath = null)
    : ResourceContainer
{
    const string PackResourceName = "assets.pack";
    static readonly byte[] PackMagic = "SBAPACK1"u8.ToArray();

    readonly string baseNamespace = baseNamespace ?? $"{assembly.EntryPoint.DeclaringType.Namespace}.Resources",
        basePath = basePath ?? "resources";

    readonly Lazy<AssetPack> embeddedPack = new(() => loadPack(assembly,
        baseNamespace ?? $"{assembly.EntryPoint.DeclaringType.Namespace}.Resources"));

    public Stream GetStream(string path, ResourceSource sources)
    {
        if (path is null) return null;

        StringBuilder searched = null;

        if (Path.IsPathRooted(path))
        {
            if ((sources & ResourceSource.Absolute) != 0)
            {
                AddSearch($"absolute file: {path}");
                if (File.Exists(path))
                    return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            else throw new InvalidOperationException($"Resource paths must be relative ({path})");
        }
        else
        {
            if ((sources & ResourceSource.Relative) != 0)
            {
                var combinedPath = basePath is not null ? Path.Combine(basePath, path) : path;
                AddSearch($"relative file: {combinedPath}");
                if (File.Exists(combinedPath))
                    return new FileStream(combinedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }

            if ((sources & ResourceSource.Embedded) != 0)
            {
                var normalizedPath = normalizePath(path);
                var packResourceName = getPackResourceName(baseNamespace);
                var pack = embeddedPack.Value;
                AddSearch(pack is null
                    ? $"embedded pack resource: {packResourceName} (missing)"
                    : $"embedded pack resource: {packResourceName}");
                AddSearch($"embedded pack entry: {normalizedPath}");

                if (pack is not null && pack.TryOpen(normalizedPath, out var stream))
                    return stream;

            }
        }

        SDL.LogWarn(LogCategory.Application,
            $"Not found: {path} ({sources}). Searched:{searched}");
        return null;

        void AddSearch(string value)
            => (searched ??= new()).Append("\n  - ").Append(value);
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

    static AssetPack loadPack(Assembly assembly, string baseNamespace)
    {
        using var stream = assembly.GetManifestResourceStream(getPackResourceName(baseNamespace));
        if (stream is null) return null;

        using MemoryStream memory = new(stream.CanSeek ? checked((int)stream.Length) : 0);
        stream.CopyTo(memory);
        return new(memory.ToArray());
    }

    static string getPackResourceName(string baseNamespace)
        => $"{baseNamespace}.{PackResourceName}";

    static string normalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];
        return normalized;
    }

    sealed class AssetPack
    {
        readonly byte[] bytes;
        readonly Dictionary<string, Entry> entries;

        public AssetPack(byte[] bytes)
        {
            this.bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            entries = new(StringComparer.OrdinalIgnoreCase);
            parseIndex();
        }

        public bool TryOpen(string path, out Stream stream)
        {
            if (!entries.TryGetValue(path, out var entry))
            {
                stream = null;
                return false;
            }

            if (entry.Method == CompressionMethod.Stored)
            {
                stream = new MemoryStream(bytes,
                    checked((int)entry.Offset),
                    entry.StoredLength,
                    writable: false,
                    publiclyVisible: true);
                return true;
            }

            var compressed = new MemoryStream(bytes,
                checked((int)entry.Offset),
                entry.StoredLength,
                writable: false,
                publiclyVisible: true);
            stream = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            return true;
        }

        void parseIndex()
        {
            var span = bytes.AsSpan();
            if (span.Length < PackMagic.Length + sizeof(int))
                throw new InvalidDataException("Embedded asset pack is truncated");
            if (!span[..PackMagic.Length].SequenceEqual(PackMagic))
                throw new InvalidDataException("Embedded asset pack has an invalid signature");

            var offset = PackMagic.Length;
            var count = readInt32(span, ref offset);
            if (count < 0)
                throw new InvalidDataException("Embedded asset pack has an invalid entry count");

            for (var i = 0; i < count; ++i)
            {
                var nameLength = readInt32(span, ref offset);
                if (nameLength <= 0 || offset + nameLength > span.Length)
                    throw new InvalidDataException("Embedded asset pack has an invalid entry name");

                var name = Encoding.UTF8.GetString(span.Slice(offset, nameLength));
                offset += nameLength;

                if (offset >= span.Length)
                    throw new InvalidDataException("Embedded asset pack has an invalid compression method");
                var method = (CompressionMethod)span[offset++];
                var dataOffset = readInt64(span, ref offset);
                var storedLength = readInt32(span, ref offset);
                var uncompressedLength = readInt32(span, ref offset);

                if (dataOffset < 0 || storedLength < 0 || uncompressedLength < 0 ||
                    dataOffset + storedLength > span.Length)
                    throw new InvalidDataException($"Embedded asset pack entry '{name}' points outside the pack");

                entries.Add(name, new(method, dataOffset, storedLength));
            }
        }

        static int readInt32(ReadOnlySpan<byte> span, ref int offset)
        {
            if (offset + sizeof(int) > span.Length)
                throw new InvalidDataException("Embedded asset pack is truncated");
            var value = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            offset += sizeof(int);
            return value;
        }

        static long readInt64(ReadOnlySpan<byte> span, ref int offset)
        {
            if (offset + sizeof(long) > span.Length)
                throw new InvalidDataException("Embedded asset pack is truncated");
            var value = BinaryPrimitives.ReadInt64LittleEndian(span[offset..]);
            offset += sizeof(long);
            return value;
        }

        readonly record struct Entry(CompressionMethod Method, long Offset, int StoredLength);
    }

    enum CompressionMethod : byte
    {
        Stored = 0,
        Deflate = 1
    }
}
