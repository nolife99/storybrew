namespace BrewLib.Audio;

using System;
using System.Buffers;
using System.Diagnostics;
using Data;
using ManagedBass;

public class AudioSample : IDisposable
{
    const int MaxSimultaneousPlayBacks = 8;

    AudioManager manager;
    int sample;

    internal AudioSample(AudioManager audioManager, string path, ResourceContainer resourceContainer)
    {
        manager = audioManager;
        Path = path;

        sample = Bass.SampleLoad(path, 0, 0, MaxSimultaneousPlayBacks, BassFlags.SampleOverrideLongestPlaying);
        if (sample != 0) return;

        using (var stream = resourceContainer?.GetStream(path, ResourceSource.Embedded))
            if (stream is not null)
            {
                var len = (int)stream.Length;
                var bytes = ArrayPool<byte>.Shared.Rent(len);
                var read = stream.Read(bytes, 0, len);

                sample = Bass.SampleLoad(bytes, 0, read, MaxSimultaneousPlayBacks, BassFlags.SampleOverrideLongestPlaying);
                ArrayPool<byte>.Shared.Return(bytes);

                if (sample != 0) return;
            }

        Trace.TraceError($"Failed to load audio sample ({path}): {Bass.LastError}");
    }

    public string Path { get; }

    public void Play(float volume = 1, float pitch = 1, float pan = 0)
    {
        if (sample == 0) return;

        AudioChannel channel = new(manager, Bass.SampleGetChannel(sample), true) { Volume = volume, Pitch = pitch, Pan = pan };

        manager.RegisterChannel(channel);

        channel.Playing = true;
    }

    #region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        if (sample != 0) Bass.SampleFree(sample);
        if (!disposing) return;

        sample = 0;
        manager = null;
        disposed = true;
    }

    ~AudioSample() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}