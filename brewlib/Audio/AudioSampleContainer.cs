namespace BrewLib.Audio;

using System;
using BrewLib.IO;
using Tiny.PooledCollections.Generic;

public sealed class AudioSampleContainer : IDisposable
{
    readonly ResourceContainer container;

    readonly AudioManager manager;
    readonly PooledDictionary<string, AudioSample> samples;
    readonly PooledDictionary<string, AudioSample>.AlternateLookup<ReadOnlySpan<char>> samplesLookup;

    public AudioSampleContainer(AudioManager manager, ResourceContainer container = null)
    {
        this.manager = manager;
        this.container = container;

        samples = new();
        samplesLookup = samples.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public AudioSample Get(scoped ReadOnlySpan<char> filename)
    {
        if (samplesLookup.TryGetValue(filename, out var sample)) return sample;

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