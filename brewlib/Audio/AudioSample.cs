namespace BrewLib.Audio;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IO;
using ManagedBass;
using Configuration = SixLabors.ImageSharp.Configuration;

public class AudioSample : IDisposable
{
    const int MaxSimultaneousPlayBacks = 8;

    readonly AudioManager manager;
    readonly int sample;

    internal AudioSample(AudioManager audioManager, string path, ResourceContainer resourceContainer)
    {
        manager = audioManager;

        sample = Bass.SampleLoad(path, 0, 0, MaxSimultaneousPlayBacks, BassFlags.SampleOverrideLongestPlaying);

        if (sample != 0) return;

        using var stream = resourceContainer?.GetStream(path, ResourceSource.Embedded);

        if (stream is null) throw new BassException(Bass.LastError);

        using (var bytes = Configuration.Default.MemoryAllocator.Allocate<byte>((int)stream.Length))
        using (bytes.Memory.Pin())
        {
            var span = bytes.Memory.Span;
            sample = Bass.SampleLoad(Unsafe.ByteOffset(ref Unsafe.NullRef<byte>(), ref MemoryMarshal.GetReference(span)),
                0,
                stream.Read(span),
                MaxSimultaneousPlayBacks,
                BassFlags.SampleOverrideLongestPlaying);
        }

        if (sample != 0) return;

        throw new BassException(Bass.LastError);
    }

    public void Play(float volume = 1, float pitch = 1, float pan = 0)
    {
        if (sample == 0) return;

        AudioChannel channel =
            new(manager, Bass.SampleGetChannel(sample), true) { Volume = volume, Pitch = pitch, Pan = pan };

        manager.RegisterChannel(channel);

        channel.Playing = true;
    }

    #region IDisposable Support

    bool disposed;

    ~AudioSample() => Bass.SampleFree(sample);

    public void Dispose()
    {
        if (!disposed) Bass.SampleFree(sample);
        GC.SuppressFinalize(this);
        disposed = true;
    }

    #endregion
}