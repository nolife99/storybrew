namespace BrewLib.Audio;

using System;
using ManagedBass;

public class AudioChannel : IDisposable
{
    int channel;

    float frequency;

    bool loop;

    float pan;

    float pitch = 1;

    bool played;

    float timeFactor = 1;

    float volume = 1;

    internal AudioChannel(AudioManager audioManager, int channel = 0, bool temporary = false)
    {
        Manager = audioManager;
        Channel = channel;
        Temporary = temporary;
    }

    public AudioManager Manager { get; }
    public float Frequency => frequency;

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
            else
                Bass.ChannelPause(channel);
        }
    }

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
            value = Math.Clamp(value, -1, 1);
            if (pan == value) return;

            pan = value;
            updatePan();
        }
    }

    public int AvailableData => channel == 0 ? 0 : Bass.ChannelGetData(channel, 0, (int)DataFlags.Available);
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
        if (disposed || !disposing) return;
        channel = 0;
        disposed = true;
        Manager.UnregisterChannel(this);
    }

    ~AudioChannel() => Dispose(false);
    public void Dispose() => Dispose(true);

#endregion
}