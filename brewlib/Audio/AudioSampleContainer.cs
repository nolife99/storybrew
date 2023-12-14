using System;
using System.Collections.Generic;
using System.Linq;
using BrewLib.Data;
using BrewLib.Util;

namespace BrewLib.Audio;

public sealed class AudioSampleContainer(AudioManager audioManager, ResourceContainer resourceContainer = null) : IDisposable
{
    Dictionary<string, AudioSample> samples = [];
    public IEnumerable<string> ResourceNames => samples.Where(e => e.Value is not null).Select(e => e.Key);

    public AudioSample Get(string filename)
    {
        filename = PathHelper.WithStandardSeparators(filename);
        if (!samples.TryGetValue(filename, out var sample))
        {
            sample = audioManager.LoadSample(filename, resourceContainer);
            samples.Add(filename, sample);
        }
        return sample;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            samples.Dispose();
            samples = null;
            disposed = true;
        }
    }

    #endregion
}