using BrewLib.Data;
using BrewLib.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BrewLib.Audio
{
    public class AudioSampleContainer(AudioManager audioManager, ResourceContainer resourceContainer = null) : IDisposable
    {
        readonly AudioManager audioManager = audioManager;
        readonly ResourceContainer resourceContainer = resourceContainer;

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

        bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var entry in samples.Values) entry?.Dispose();
                    samples.Clear();
                }
                samples = null;
                disposedValue = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}