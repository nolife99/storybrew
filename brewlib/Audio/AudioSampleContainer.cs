namespace BrewLib.Audio;

using System;
using IO;
using Tiny.PooledCollections.Generic;

public sealed class AudioSampleContainer(AudioManager manager, ResourceContainer container = null) : IDisposable
{
    readonly PooledDictionary<string, AudioSample> samples = new();

    public AudioSample Get(string filename)
    {
        if (samples.TryGetValue(filename, out var sample)) return sample;

        return samples[filename] = manager.LoadSample(filename, container);
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