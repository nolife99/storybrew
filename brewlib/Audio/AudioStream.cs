using BrewLib.Data;
using ManagedBass;
using ManagedBass.Fx;
using System;
using System.Diagnostics;
using System.IO;

namespace BrewLib.Audio
{
    public class AudioStream : AudioChannel
    {
        int stream;
        int decodeStream;

        public string Path { get; }

        internal unsafe AudioStream(AudioManager manager, string path, ResourceContainer resourceContainer) : base(manager)
        {
            Path = path;
            var flags = BassFlags.Decode | BassFlags.Prescan;

            decodeStream = Bass.CreateStream(path, 0, 0, flags);
            if (decodeStream == 0 && !System.IO.Path.IsPathRooted(path))
            {
                var resourceStream = resourceContainer.GetStream(path, ResourceSource.Embedded);
                if (resourceStream is not null)
                {
                    var procedures = new FileProcedures
                    {
                        Read = (buffer, length, user) =>
                        {
                            Span<byte> readBuffer = stackalloc byte[length];
                            var readBytes = resourceStream.Read(readBuffer);
                            readBuffer.CopyTo(new Span<byte>(buffer.ToPointer(), readBytes));
                            return readBytes;
                        },
                        Length = user => resourceStream.Length,
                        Seek = (offset, user) => resourceStream.Seek(offset, SeekOrigin.Begin) == offset,
                        Close = user => resourceStream.Dispose()
                    };
                    decodeStream = Bass.CreateStream(StreamSystem.NoBuffer, flags, procedures);
                }
            }
            if (decodeStream == 0)
            {
                Trace.WriteLine($"Failed to load audio stream ({path}): {Bass.LastError}");
                return;
            }

            stream = BassFx.TempoCreate(decodeStream, BassFlags.Default);
            Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoUseQuickAlgorithm, 1);
            Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoOverlapMilliseconds, 4);
            Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoSequenceMilliseconds, 30);

            Channel = stream;
        }

        #region IDisposable Support

        bool disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
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
                disposedValue = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}