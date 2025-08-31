namespace BrewLib.Audio;

using System.IO;
using BrewLib.IO;
using BrewLib.Util;
using ManagedBass;
using ManagedBass.Fx;
using SDL3;

public class AudioStream : AudioChannel
{
    int stream, decodeStream;

    internal AudioStream(AudioManager manager, string path, ResourceContainer resourceContainer) : base(manager)
    {
        const BassFlags flags = BassFlags.Decode;

        decodeStream = Bass.CreateStream(path, 0, 0, flags | BassFlags.AsyncFile);
        if (decodeStream == 0 && !Path.IsPathRooted(path))
        {
            var resourceStream = resourceContainer.GetStream(path, ResourceSource.Embedded);

            if (resourceStream is not null)
                decodeStream = Bass.CreateStream(StreamSystem.NoBuffer,
                    flags,
                    new()
                    {
                        Read =
                            (buffer, _, _) => resourceStream.Read(buffer.AsSpan<byte>((int)resourceStream.Length)),
                        Length = _ => resourceStream.Length,
                        Seek = (offset, _) => resourceStream.Seek(offset, SeekOrigin.Begin) == offset,
                        Close = _ => resourceStream.Dispose()
                    });
        }

        if (decodeStream == 0)
        {
            SDL.LogError(SDL.LogCategory.Audio, $"Loading audio stream ({path}): {Bass.LastError}");

            return;
        }

        stream = BassFx.TempoCreate(decodeStream, BassFlags.FxTempoAlgorithmLinear);
        Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoUseQuickAlgorithm, 1);
        Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoUseAAFilter, 0);
        Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoPreventClick, 1);

        Channel = stream;
    }

    #region IDisposable Support

    bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (stream != 0)
            {
                Bass.StreamFree(stream);
                stream = 0;
            }

            if (decodeStream != 0)
            {
                Bass.StreamFree(decodeStream);
                decodeStream = 0;
            }

            disposed = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}