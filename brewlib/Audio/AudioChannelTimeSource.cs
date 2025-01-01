namespace BrewLib.Audio;

using Time;

public class AudioChannelTimeSource(AudioChannel channel) : TimeSource
{
    public float Current => channel.Time;

    public float TimeFactor { get => channel.TimeFactor; set => channel.TimeFactor = value; }

    public bool Playing { get => channel.Playing; set => channel.Playing = value; }

    public bool Seek(float time)
    {
        if (time < 0 || time >= channel.Duration) return false;

        channel.Time = time;
        return true;
    }
}