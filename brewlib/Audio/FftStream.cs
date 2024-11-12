namespace BrewLib.Audio;

using System;
using ManagedBass;

public class FftStream : IDisposable
{
    public readonly float Duration, Frequency;
    ChannelInfo info;
    int stream;

    public FftStream(string path)
    {
        stream = Bass.CreateStream(path, 0, 0, BassFlags.Decode | BassFlags.Prescan);
        Duration = (float)Bass.ChannelBytes2Seconds(stream, Bass.ChannelGetLength(stream));
        info = Bass.ChannelGetInfo(stream);

        Bass.ChannelGetAttribute(stream, ChannelAttribute.Frequency, out Frequency);
    }

    public float[] GetFft(float time, bool splitChannels = false)
    {
        Bass.ChannelSetPosition(stream, Bass.ChannelSeconds2Bytes(stream, time));

        var size = 1024;
        var flags = DataFlags.FFT2048;

        if (splitChannels)
        {
            size *= info.Channels;
            flags |= DataFlags.FFTIndividual;
        }

        var data = GC.AllocateUninitializedArray<float>(size);
        if (Bass.ChannelGetData(stream, data, (int)flags) == -1) throw new BassException(Bass.LastError);
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