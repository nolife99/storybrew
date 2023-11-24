﻿using BrewLib.Data;
using ManagedBass;
using ManagedBass.Fx;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BrewLib.Audio
{
    public class AudioStream : AudioChannel
    {
        int stream, decodeStream;
        internal AudioStream(AudioManager manager, string path, ResourceContainer resourceContainer) : base(manager)
        {
            var flags = BassFlags.Decode | BassFlags.Prescan;

            decodeStream = Bass.CreateStream(path, 0, 0, flags);
            if (decodeStream == 0 && !Path.IsPathRooted(path))
            {
                var resourceStream = resourceContainer.GetStream(path, ResourceSource.Embedded);
                if (resourceStream is not null)
                {
                    FileProcedures procedures = new()
                    {
                        Read = (buffer, length, user) =>
                        {
                            Span<byte> readBuffer = stackalloc byte[length];
                            var readBytes = resourceStream.Read(readBuffer);
                            unsafe
                            {
                                Unsafe.CopyBlock(ref *(byte*)buffer, ref MemoryMarshal.GetReference(readBuffer), (uint)readBytes);
                            }

                            readBuffer.Clear();
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