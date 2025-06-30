namespace BrewLib.Audio;

using System;
using IO;
using Tiny.PooledCollections.Generic;

public sealed class AudioSampleContainer(AudioManager manager, ResourceContainer container = null) : IDisposable
{
    readonly PooledDictionary<string, AudioSample> samples = new();

    public AudioSample Get(scoped ReadOnlySpan<char> filename)
    {
        if (samples.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(filename, out var sample)) return sample;

        var str = filename.ToString();
        return samples[str] = manager.LoadSample(str, container);
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var sample in samples.Values) sample.Dispose();
        samples.Dispose();
        disposed = true;
    }

    #endregion
}