using BrewLib.Time;

namespace BrewLib.Audio;

public class AudioChannelTimeSource(AudioChannel channel) : TimeSource
{
    public float Current => channel.Time;
    public bool Playing
    {
        get => channel.Playing;
        set => channel.Playing = value;
    }
    public float TimeFactor
    {
        get => channel.TimeFactor;
        set => channel.TimeFactor = value;
    }

    public bool Seek(float time)
    {
        if (time >= 0 && time < channel.Duration)
        {
            channel.Time = time;
            return true;
        }
        return false;
    }
}