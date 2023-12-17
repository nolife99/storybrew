using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BrewLib.Data;
using BrewLib.Util;
using ManagedBass;

namespace BrewLib.Audio;

public sealed class AudioManager : IDisposable
{
    readonly List<AudioChannel> audioChannels = [];

    float volume = 1;
    public float Volume
    {
        get => volume;
        set
        {
            if (volume == value) return;

            volume = value;
            audioChannels.ForEachUnsafe(audio => audio.UpdateVolume());
        }
    }
    public AudioManager(nint handle)
    {
        Bass.Init(Win: handle);
        Bass.PlaybackBufferLength = 100;
        Bass.NetBufferLength = 500;
        Bass.UpdatePeriod = 10;
    }

    public void Update()
    {
        var span = CollectionsMarshal.AsSpan(audioChannels);
        for (var i = 0; i < span.Length; ++i)
        {
            var channel = span[i];
            if (channel is not null && channel.Temporary && channel.Completed)
            {
                channel.Dispose();
                --i;
            }
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
    void dispose()
    {
        if (!disposed)
        {
            Bass.Free();
            disposed = true;
        }
    }

    ~AudioManager() => dispose();
    public void Dispose()
    {
        dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}