namespace BrewLib.Audio;

using System;
using System.Buffers;
using ManagedBass;
using SixLabors.ImageSharp.Memory;

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

    public unsafe IMemoryOwner<float> GetFft(float time, bool splitChannels = false)
    {
        Bass.ChannelSetPosition(stream, Bass.ChannelSeconds2Bytes(stream, time));

        var size = 1024;
        var flags = DataFlags.FFT2048;

        if (splitChannels)
        {
            size *= info.Channels;
            flags |= DataFlags.FFTIndividual;
        }

        var data = MemoryAllocator.Default.Allocate<float>(size);
        fixed (void* pinned = data.Memory.Span)
            if (Bass.ChannelGetData(stream, (nint)pinned, (int)flags) == -1)
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