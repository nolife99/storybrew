namespace BrewLib.Time;

using System;
using System.Diagnostics;

public sealed class Clock : TimeSource
{
    readonly Stopwatch stopwatch = new();

    bool playing;
    float timeFactor = 1;

    TimeSpan timeOrigin;
    public TimeSpan Current => timeOrigin + stopwatch.Elapsed * timeFactor;

    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            var elapsed = stopwatch.Elapsed;
            var previousTime = timeOrigin + elapsed * timeFactor;
            timeFactor = value;
            timeOrigin = previousTime - elapsed * timeFactor;
        }
    }

    public bool Playing
    {
        get => playing;
        set
        {
            if (playing == value) return;

            playing = value;

            if (playing) stopwatch.Start();
            else stopwatch.Stop();
        }
    }

    public bool Seek(TimeSpan time)
    {
        timeOrigin = time - stopwatch.Elapsed * timeFactor;
        return true;
    }
}