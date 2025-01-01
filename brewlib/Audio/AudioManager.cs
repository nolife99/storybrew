namespace BrewLib.Audio;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using IO;
using ManagedBass;

public sealed class AudioManager : IDisposable
{
    readonly List<AudioChannel> audioChannels = [];
    float volume = 1;

    public AudioManager(nint handle)
    {
        Trace.WriteLine($"Initializing audio - Bass {Bass.Version}");
        if (Bass.Init(Win: handle)) return;

        Trace.WriteLine($"Failed to initialize audio with default device: {Bass.LastError}");

        var initialized = false;
        for (var i = 0; i < Bass.DeviceCount; ++i)
        {
            var device = Bass.GetDeviceInfo(i);
            if (device.Driver is null || device.IsDefault) continue;

            if (Bass.Init(i, Win: handle))
            {
                initialized = true;
                break;
            }

            Trace.WriteLine($"Failed to initialize audio with device {i}: {Bass.LastError}");
        }

        if (!initialized) throw new BassException(Bass.LastError);
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
            if (!channel.Temporary || !channel.Completed) continue;

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

    public AudioSample LoadSample(string path, ResourceContainer resourceContainer = null)
        => new(this, path, resourceContainer);

    internal void RegisterChannel(AudioChannel channel) => audioChannels.Add(channel);
    internal void UnregisterChannel(AudioChannel channel) => audioChannels.Remove(channel);

    #region IDisposable Support

    bool disposed;

    ~AudioManager() => Bass.Free();

    public void Dispose()
    {
        if (disposed) return;

        Bass.Free();
        disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}