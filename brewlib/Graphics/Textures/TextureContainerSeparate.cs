using BrewLib.Data;
using BrewLib.Util;
using System.Collections.Generic;
using System.Linq;

namespace BrewLib.Graphics.Textures
{
    public class TextureContainerSeparate(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null) : TextureContainer
    {
        readonly ResourceContainer resourceContainer = resourceContainer;
        readonly TextureOptions textureOptions = textureOptions;

        Dictionary<string, Texture2d> textures = [];

        public IEnumerable<string> ResourceNames => textures.Where(e => e.Value is not null).Select(e => e.Key);
        public event ResourceLoadedDelegate<Texture2dRegion> ResourceLoaded;

        public Texture2dRegion Get(string filename)
        {
            filename = PathHelper.WithStandardSeparators(filename);
            if (!textures.TryGetValue(filename, out Texture2d texture))
            {
                textures.Add(filename, texture = Texture2d.Load(filename, resourceContainer, textureOptions));
                ResourceLoaded?.Invoke(filename, texture);
            }
            return texture;
        }

        #region IDisposable Support

        bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var entry in textures.Values) entry?.Dispose();
                    textures.Clear();
                }
                textures = null;
                disposedValue = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}