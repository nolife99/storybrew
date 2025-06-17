namespace BrewLib.Audio;

using System;
using IO;
using Tiny.PooledCollections.Generic;

public sealed class AudioSampleContainer(AudioManager manager, ResourceContainer container = null) : IDisposable
{
    readonly PooledDictionary<int, AudioSample> samples = new();

    public AudioSample Get(scoped ReadOnlySpan<char> filename)
    {
        var hashCode = string.GetHashCode(filename);
        if (samples.TryGetValue(hashCode, out var sample)) return sample;

        return samples[hashCode] = manager.LoadSample(filename.ToString(), container);
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