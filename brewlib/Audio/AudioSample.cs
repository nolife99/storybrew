using System;
using System.Diagnostics;
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

        var bytes = resourceContainer?.GetBytes(path, ResourceSource.Embedded);
        if (bytes is not null)
        {
            sample = Bass.SampleLoad(bytes, 0, bytes.Length, MaxSimultaneousPlayBacks, BassFlags.SampleOverrideLongestPlaying);
            if (sample != 0) return;
        }

        Trace.WriteLine($"Failed to load audio sample ({path}): {Bass.LastError}");
    }
    public void Play(float volume = 1, float pitch = 1, float pan = 0)
    {
        if (sample == 0) return;
        var channel = new AudioChannel(manager, Bass.SampleGetChannel(sample), true)
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
                sample = 0;
            }
            disposed = true;
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