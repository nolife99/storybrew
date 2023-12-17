using System;

namespace BrewLib.Time;

public class TimeSourceExtender(TimeSource timeSource) : TimeSource
{
    readonly Clock clock = new();
    public float Current => timeSource.Playing ? timeSource.Current : clock.Current;

    public bool Playing
    {
        get => clock.Playing;
        set
        {
            if (clock.Playing == value) return;

            timeSource.Playing = value && timeSource.Seek(clock.Current);
            clock.Playing = value;
        }
    }
    public float TimeFactor
    {
        get => clock.TimeFactor;
        set
        {
            timeSource.TimeFactor = value;
            clock.TimeFactor = value;
        }
    }

    public bool Seek(float time)
    {
        if (!timeSource.Seek(time)) timeSource.Playing = false;
        return clock.Seek(time);
    }
    public void Update()
    {
        timeSource.Playing = clock.Playing && (timeSource.Playing || timeSource.Seek(clock.Current));
        if (timeSource.Playing && Math.Abs(clock.Current - timeSource.Current) > .005f) clock.Seek(timeSource.Current);
    }
}