using System;
using ManagedBass;

namespace BrewLib.Audio;

public class FftStream : IDisposable
{
    int stream;
    ChannelInfo info;

    readonly float frequency;
    public float Frequency => frequency;

    public double Duration { get; }

    public FftStream(string path)
    {
        stream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Prescan);
        Duration = Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream));
        info = Bass.ChannelGetInfo(stream);

        Bass.ChannelGetAttribute(stream, ChannelAttribute.Frequency, out frequency);
    }

    public unsafe Span<float> GetFft(double time, bool splitChannels = false)
    {
        Bass.ChannelSetPosition(stream, Bass.ChannelSeconds2Bytes(stream, time));

        var size = 1024;
        var flags = DataFlags.FFT2048;

        if (splitChannels)
        {
            size *= info.Channels;
            flags |= DataFlags.FFTIndividual;
        }

        Span<float> data = GC.AllocateUninitializedArray<float>(size);
        fixed (void* pinned = &data[0]) if (Bass.ChannelGetData(stream, (nint)pinned, unchecked((int)flags)) == -1) throw new BassException(Bass.LastError);
        return data;
    }

    #region IDisposable Support

    ~FftStream() => Dispose(false);

    bool disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            Bass.StreamFree(stream);
            if (disposing)
            {
                stream = 0;
                disposed = true;
            }
        }
    }

    #endregion
}