using System.Diagnostics;

namespace BrewLib.Time;

public class Clock : TimeSource
{
    readonly Stopwatch stopwatch = new();
    float timeOrigin;

    public float Current => timeOrigin + (float)stopwatch.Elapsed.TotalSeconds * timeFactor;

    float timeFactor = 1;
    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            var elapsed = (float)stopwatch.Elapsed.TotalSeconds;
            timeFactor = value;
            timeOrigin = (timeOrigin + elapsed * timeFactor) - elapsed * timeFactor;
        }
    }

    bool playing;
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
        timeOrigin = time - (float)stopwatch.Elapsed.TotalSeconds * timeFactor;
        return true;
    }
}