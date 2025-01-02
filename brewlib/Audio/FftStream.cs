namespace BrewLib.Audio;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ManagedBass;
using Configuration = SixLabors.ImageSharp.Configuration;

public class FftStream : IDisposable
{
    public readonly float Duration, Frequency;
    readonly int stream;
    ChannelInfo info;

    public FftStream(string path)
    {
        stream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Prescan);
        Duration = (float)Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream));
        info = Bass.ChannelGetInfo(stream);

        Bass.ChannelGetAttribute(stream, ChannelAttribute.Frequency, out Frequency);
    }

    public IMemoryOwner<float> GetFft(float time, bool splitChannels = false)
    {
        Bass.ChannelSetPosition(stream, Bass.ChannelSeconds2Bytes(stream, time));

        var size = 1024;
        var flags = DataFlags.FFT2048;

        if (splitChannels)
        {
            size *= info.Channels;
            flags |= DataFlags.FFTIndividual;
        }

        var data = Configuration.Default.MemoryAllocator.Allocate<float>(size);
        using (data.Memory.Pin())
            if (Bass.ChannelGetData(stream,
                    Unsafe.ByteOffset(ref Unsafe.NullRef<float>(), ref MemoryMarshal.GetReference(data.Memory.Span)),
                    (int)flags) ==
                -1)
                throw new BassException(Bass.LastError);

        return data;
    }

    #region IDisposable Support

    bool disposed;

    void Dispose(bool disposing)
    {
        if (disposed) return;

        Bass.StreamFree(stream);

        if (disposing) disposed = true;
    }

    ~FftStream() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}