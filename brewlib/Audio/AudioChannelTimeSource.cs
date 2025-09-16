namespace BrewLib.Audio;

using System;
using BrewLib.Time;

public sealed class AudioChannelTimeSource(AudioChannel channel) : TimeSource
{
    public TimeSpan Current => channel.Time;

    public float TimeFactor { get => channel.TimeFactor; set => channel.TimeFactor = value; }

    public bool Playing { get => channel.Playing; set => channel.Playing = value; }

    public bool Seek(TimeSpan time)
    {
        if (time < TimeSpan.Zero || time >= channel.Duration) return false;

        channel.Time = time;
        return true;
    }
}