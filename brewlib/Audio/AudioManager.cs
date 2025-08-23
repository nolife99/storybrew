namespace BrewLib.Audio;

using System;
using System.Diagnostics;
using BrewLib.IO;
using ManagedBass;
using Tiny.PooledCollections.Generic;

public sealed class AudioManager : IDisposable
{
    readonly PooledList<AudioChannel> audioChannels = new();
    float volume = 1;

    public AudioManager()
    {
        const DeviceInitFlags flags = DeviceInitFlags.DirectSound | DeviceInitFlags.Latency;

        var initialized = false;
        try
        {
            Trace.WriteLine($"Initializing audio - Bass {Bass.Version}");
            if (Bass.Init(Flags: flags))
            {
                initialized = true;
                return;
            }

            Trace.WriteLine($"Failed to initialize audio with default device: {Bass.LastError}");

            for (var i = 0; i < Bass.DeviceCount; ++i)
            {
                var device = Bass.GetDeviceInfo(i);
                if (device.Driver is null || device.IsDefault) continue;

                if (Bass.Init(i, Flags: flags))
                {
                    initialized = true;
                    return;
                }

                Trace.WriteLine($"Failed to initialize audio with device {i}: {Bass.LastError}");
            }
        }
        finally
        {
            if (initialized) Bass.UpdateThreads = 0;

            Bass.GetInfo(out var info);
            Bass.PlaybackBufferLength = info.MinBufferLength * 3;
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

    public void Update(long targetFrame)
    {
        for (var i = 0; i < audioChannels.Count; ++i)
        {
            var channel = audioChannels[i];
            if (!channel.Completed && channel.Playing)
                Bass.ChannelUpdate(channel.Channel, (int)(targetFrame * 1.5f / TimeSpan.TicksPerMillisecond));

            if (!channel.Temporary || !channel.Completed)
            {
                if (channel.Playing && Bass.GetDeviceInfo(Bass.ChannelGetDevice(channel.Channel), out var info) &&
                    !SoundUtil.IsDefault(info))
                {
                    var device = 0;
                    while (Bass.GetDeviceInfo(device, out info))
                    {
                        if (info.Driver is not null && SoundUtil.IsDefault(info)) break;

                        ++device;
                    }

                    Bass.ChannelSetDevice(channel.Channel, device);
                }

                continue;
            }

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

        audioChannels.Dispose();

        Bass.Free();
        disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}