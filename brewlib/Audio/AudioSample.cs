using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BrewLib.Data;
using ManagedBass;

namespace BrewLib.Audio;

public class AudioSample : IDisposable
{
    const int MaxSimultaneousPlayBacks = 8;
    int sample;

    readonly AudioManager manager;
    public string Path { get; }

    internal AudioSample(AudioManager audioManager, string path, ResourceContainer resourceContainer)
    {
        manager = audioManager;
        Path = path;

        sample = Bass.SampleLoad(path, 0, 0, MaxSimultaneousPlayBacks, BassFlags.SampleOverrideLongestPlaying);
        if (sample != 0) return;

        using (var stream = resourceContainer?.GetStream(path, ResourceSource.Embedded)) if (stream is not null) unsafe
        {
            var len = (int)stream.Length;
            var bytes = ArrayPool<byte>.Shared.Rent(len);

            try
            {
                stream.Read(bytes, 0, len);
                sample = Bass.SampleLoad(bytes, 0, len, MaxSimultaneousPlayBacks, BassFlags.SampleOverrideLongestPlaying);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
            if (sample != 0) return;
        }

        Trace.WriteLine($"Failed to load audio sample ({path}): {Bass.LastError}");
    }
    public void Play(float volume = 1, float pitch = 1, float pan = 0)
    {
        if (sample == 0) return;

        AudioChannel channel = new(manager, Bass.SampleGetChannel(sample), true)
        {
            Volume = volume,
            Pitch = pitch,
            Pan = pan
        };
        manager.RegisterChannel(channel);

        channel.Playing = true;
    }

    #region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (sample != 0)
            {
                Bass.SampleFree(sample);
                if (disposing) sample = 0;
            }
            if (disposing) disposed = true;
        }
    }

    ~AudioSample() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}