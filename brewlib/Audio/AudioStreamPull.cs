using ManagedBass;
using ManagedBass.Fx;
using System.Diagnostics;

namespace BrewLib.Audio
{
    public class AudioStreamPull : AudioChannel
    {
        public delegate int CallbackDelegate(nint buffer, int sampleCount);

        readonly CallbackDelegate callback;
        readonly int bytesPerSample;
        int stream, decodeStream;

        internal AudioStreamPull(AudioManager manager, int frequency, int channels, CallbackDelegate callback) : base(manager)
        {
            this.callback = callback;
            bytesPerSample = sizeof(short);

            var flags = BassFlags.Decode;
            if (channels == 1) flags |= BassFlags.Mono;

            decodeStream = Bass.CreateStream(frequency, channels, flags, streamProcedure);
            if (decodeStream == 0)
            {
                Trace.WriteLine($"Failed to create pull audio stream: {Bass.LastError}");
                return;
            }

            stream = BassFx.TempoCreate(decodeStream, BassFlags.Default);
            Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoUseQuickAlgorithm, 1);
            Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoOverlapMilliseconds, 4);
            Bass.ChannelSetAttribute(stream, ChannelAttribute.TempoSequenceMilliseconds, 30);

            Channel = stream;
        }
        int streamProcedure(int handle, nint buffer, int byteLength, nint user)
        {
            var samples = byteLength / bytesPerSample;
            var writtenSamples = callback(buffer, samples);
            if (writtenSamples == int.MinValue) return int.MinValue;
            return writtenSamples * bytesPerSample;
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