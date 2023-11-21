using BrewLib.Util;
using System.Collections.Generic;
using System.IO;

namespace BrewLib.Data
{
    public class SubResourceContainer(ResourceContainer resourceContainer, string path) : ResourceContainer
    {
        readonly ResourceContainer resourceContainer = resourceContainer;
        readonly string path = path;

        public IEnumerable<string> ResourceNames => resourceContainer.ResourceNames;

        public Stream GetStream(string path, ResourceSource sources)
            => resourceContainer.GetStream(applyPath(path), sources) ?? resourceContainer.GetStream(path, sources);

        public byte[] GetBytes(string path, ResourceSource sources)
            => resourceContainer.GetBytes(applyPath(path), sources) ?? resourceContainer.GetBytes(path, sources);

        public string GetString(string path, ResourceSource sources)
            => resourceContainer.GetString(applyPath(path), sources) ?? resourceContainer.GetString(path, sources);

        public SafeWriteStream GetWriteStream(string path)
            => resourceContainer.GetWriteStream(applyPath(path));

        string applyPath(string filename)
            => Path.Combine(path, filename);
    }
}