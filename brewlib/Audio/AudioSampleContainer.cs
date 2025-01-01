namespace BrewLib.Audio;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using IO;
using Util;

public sealed class AudioSampleContainer(AudioManager manager, ResourceContainer container = null) : IDisposable
{
    readonly Dictionary<string, AudioSample> samples = [];

    public AudioSample Get(string filename)
    {
        ref var sample = ref CollectionsMarshal.GetValueRefOrAddDefault(samples, filename, out var exists);

        if (!exists) sample = manager.LoadSample(filename, container);

        return sample;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        samples.Dispose();
        disposed = true;
    }

    #endregion
}