using System;
using ManagedBass;

namespace BrewLib.Audio;

public class AudioChannel : IDisposable
{
    public AudioManager Manager { get; }

    float frequency;
    public float Frequency => frequency;

    int channel;
    protected int Channel
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
        get => channel != 0 ? (float)Bass.ChannelBytes2Seconds(channel, Bass.ChannelGetPosition(channel, PositionFlags.Bytes)) : 0;
        set
        {
            if (channel == 0) return;
            Bass.ChannelSetPosition(channel, Bass.ChannelSeconds2Bytes(channel, value));
        }
    }
    public float Duration { get; set; }

    bool played;
    public bool Playing
    {
        get
        {
            if (channel == 0) return false;
            var playbackState = Bass.ChannelIsActive(channel);
            return playbackState == PlaybackState.Playing || playbackState == PlaybackState.Stalled;
        }
        set
        {
            if (channel == 0) return;
            if (value)
            {
                Bass.ChannelPlay(channel, false);
                played = true;
            }
            else Bass.ChannelPause(channel);
        }
    }

    bool loop;
    public bool Loop
    {
        get => loop;
        set
        {
            if (loop == value) return;
            loop = value;
            if (channel == 0) return;
            Bass.ChannelFlags(channel, loop ? BassFlags.Loop : 0, BassFlags.Loop);
        }
    }

    public bool Completed => played && Bass.ChannelIsActive(channel) == PlaybackState.Stopped;

    float volume = 1;
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

    float timeFactor = 1;
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

    float pitch = 1;
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

    float pan;
    public float Pan
    {
        get => pan;
        set
        {
            value = Math.Clamp(value, -1, 1);
            if (pan == value) return;

            pan = value;
            updatePan();
        }
    }

    public int AvailableData => channel == 0 ? 0 : Bass.ChannelGetData(channel, 0, (int)DataFlags.Available);
    public bool Temporary { get; }

    internal AudioChannel(AudioManager audioManager, int channel = 0, bool temporary = false)
    {
        Manager = audioManager;
        Channel = channel;
        Temporary = temporary;
    }

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
        Bass.ChannelSetAttribute(channel, ChannelAttribute.Frequency, Math.Clamp(frequency * pitch, 100, 100000));
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
        if (!disposed)
        {
            channel = 0;
            disposed = true;
            if (disposing) Manager.UnregisterChannel(this);
        }
    }

    ~AudioChannel() => Dispose(false);
    public void Dispose() => Dispose(true);

    #endregion
}