﻿namespace BrewLib.Audio;

using System;
using System.Collections.Generic;
using Data;
using Util;

public sealed class AudioSampleContainer(AudioManager manager, ResourceContainer container = null) : IDisposable
{
    readonly Dictionary<string, AudioSample> samples = [];

    public AudioSample Get(string filename)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);
        if (!samples.TryGetValue(filename, out var sample)) samples[filename] = sample = manager.LoadSample(filename, container);

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