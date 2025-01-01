namespace BrewLib.Audio;

using System;
using ManagedBass;

public class AudioChannel : IDisposable
{
    readonly AudioManager Manager;
    int channel;
    float frequency, pan, pitch = 1, timeFactor = 1, volume = 1;
    bool played;

    internal AudioChannel(AudioManager audioManager, int channel = 0, bool temporary = false)
    {
        Manager = audioManager;
        Channel = channel;
        Temporary = temporary;
    }

    public int Channel
    {
        get => channel;
        set
        {
            if (channel == value) return;

            channel = value;

            if (channel == 0) return;

            Bass.ChannelGetAttribute(channel, ChannelAttribute.Frequency, out frequency);
            Duration = (float)Bass.ChannelBytes2Seconds(channel, Bass.ChannelGetLength(channel));

            UpdateVolume();
            updateTimeFactor();
        }
    }

    public float Time
    {
        get => channel != 0 ? (float)Bass.ChannelBytes2Seconds(channel, Bass.ChannelGetPosition(channel)) : 0;
        set
        {
            if (channel == 0) return;

            Bass.ChannelSetPosition(channel, Bass.ChannelSeconds2Bytes(channel, value));
        }
    }

    public float Duration { get; set; }

    public bool Playing
    {
        get
        {
            if (channel == 0) return false;

            var playbackState = Bass.ChannelIsActive(channel);
            return playbackState is PlaybackState.Playing or PlaybackState.Stalled;
        }
        set
        {
            if (channel == 0) return;

            if (value)
            {
                Bass.ChannelPlay(channel);
                played = true;
            }
            else Bass.ChannelPause(channel);
        }
    }

    public bool Completed => played && Bass.ChannelIsActive(channel) == PlaybackState.Stopped;

    public float Volume
    {
        get => volume;
        set
        {
            if (volume == value) return;

            volume = value;
            UpdateVolume();
        }
    }

    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            timeFactor = value;
            updateTimeFactor();
        }
    }

    public float Pitch
    {
        get => pitch;
        set
        {
            if (pitch == value) return;

            pitch = value;
            updatePitch();
        }
    }

    public float Pan
    {
        get => pan;
        set
        {
            value = float.Clamp(value, -1, 1);
            if (pan == value) return;

            pan = value;
            updatePan();
        }
    }

    public bool Temporary { get; }

    internal void UpdateVolume()
    {
        if (channel == 0) return;

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, SoundUtil.FromLinearVolume(volume * Manager.Volume));
    }

    void updateTimeFactor()
    {
        if (channel == 0) return;

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Tempo, (timeFactor - 1) * 100);
    }

    void updatePitch()
    {
        if (channel == 0 || frequency <= 0) return;

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Frequency, float.Clamp(frequency * pitch, 100, 100000));
    }

    void updatePan()
    {
        if (channel == 0) return;

        Bass.ChannelSetAttribute(channel, ChannelAttribute.Pan, pan);
    }

    #region IDisposable Support

    bool disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (disposed || !disposing) return;

        Manager.UnregisterChannel(this);
        disposed = true;
    }

    ~AudioChannel() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}