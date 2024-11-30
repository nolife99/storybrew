namespace BrewLib.Time;

using System;
using System.Diagnostics;

public class Clock : TimeSource
{
    readonly Stopwatch stopwatch = new();

    bool playing;
    float timeFactor = 1, timeOrigin;

    public float Current => timeOrigin + stopwatch.ElapsedTicks / (float)TimeSpan.TicksPerSecond * timeFactor;

    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            var elapsed = stopwatch.ElapsedTicks / (float)TimeSpan.TicksPerSecond;
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

    public bool Seek(float time)
    {
        timeOrigin = time - stopwatch.ElapsedTicks / (float)TimeSpan.TicksPerSecond * timeFactor;
        return true;
    }
}