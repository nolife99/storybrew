namespace BrewLib.Data;

using System;
using System.Collections.Generic;
using System.IO;
using Util;

public interface ResourceContainer
{
    IEnumerable<string> ResourceNames { get; }

    Stream GetStream(string path, ResourceSource sources);
    byte[] GetBytes(string path, ResourceSource sources);
    string GetString(string path, ResourceSource sources);

    SafeWriteStream GetWriteStream(string path);
}

[Flags] public enum ResourceSource
{
    Embedded = 1, Relative = 2, Absolute = 4,
    None = 0, Local = Embedded | Relative, Any = Embedded | Relative | Absolute
}