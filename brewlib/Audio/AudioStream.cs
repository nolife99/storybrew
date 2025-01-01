namespace BrewLib.Audio;

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IO;
using ManagedBass;
using ManagedBass.Fx;

public class AudioStream : AudioChannel
{
    int stream, decodeStream;

    internal AudioStream(AudioManager manager, string path, ResourceContainer resourceContainer) : base(manager)
    {
        const BassFlags flags = BassFlags.Decode | BassFlags.Prescan;

        decodeStream = Bass.CreateStream(path, 0, 0, flags);
        if (decodeStream == 0 && !Path.IsPathRooted(path))
        {
            var resourceStream = resourceContainer.GetStream(path, ResourceSource.Embedded);

            if (resourceStream is not null)
                decodeStream = Bass.CreateStream(
                    StreamSystem.NoBuffer,
                    flags,
                    new()
                    {
                        Read = (buffer, _, _) => resourceStream.Read(
                            MemoryMarshal.CreateSpan(
                                ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), buffer),
                                (int)resourceStream.Length)),
                        Length = _ => resourceStream.Length,
                        Seek = (offset, _) => resourceStream.Seek(offset, SeekOrigin.Begin) == offset,
                        Close = _ => resourceStream.Dispose()
                    });
        }

        if (decodeStream == 0)
        {
            Trace.TraceError($"Loading audio stream ({path}): {Bass.LastError}");

            return;
        }

        stream = BassFx.TempoCreate(decodeStream, BassFlags.Default);
        Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoUseQuickAlgorithm, 1);

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