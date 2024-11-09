namespace BrewLib.Audio;

using System;
using System.Collections.Generic;
using Data;
using ManagedBass;

public sealed class AudioManager : IDisposable
{
    readonly List<AudioChannel> audioChannels = [];
    float volume = 1;

    public AudioManager(nint handle)
    {
        Bass.Init(Win: handle);
        Bass.PlaybackBufferLength = 100;
        Bass.NetBufferLength = 500;
        Bass.UpdatePeriod = 10;
    }

    public float Volume
    {
        get => volume;
        set
        {
            if (volume == value) return;

            volume = value;
            foreach (var channel in audioChannels) channel.UpdateVolume();
        }
    }

    public void Update()
    {
        for (var i = 0; i < audioChannels.Count; ++i)
        {
            var channel = audioChannels[i];
            if (channel is null || !channel.Temporary || !channel.Completed) continue;
            channel.Dispose();
            --i;
        }
    }

    public AudioStream LoadStream(string path, ResourceContainer resourceContainer = null)
    {
        AudioStream audio = new(this, path, resourceContainer);
        RegisterChannel(audio);
        return audio;
    }

    public AudioSample LoadSample(string path, ResourceContainer resourceContainer = null) => new(this, path, resourceContainer);

    internal void RegisterChannel(AudioChannel channel) => audioChannels.Add(channel);
    internal void UnregisterChannel(AudioChannel channel) => audioChannels.Remove(channel);

    #region IDisposable Support

    bool disposed;

    ~AudioManager() => Dispose();

    public void Dispose()
    {
        if (!disposed)
        {
            Bass.Free();
            disposed = true;
        }
    }

    #endregion
}