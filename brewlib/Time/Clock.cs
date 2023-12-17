using System.Diagnostics;

namespace BrewLib.Time;

public class Clock : TimeSource
{
    readonly Stopwatch stopwatch = new();
    float timeOrigin;

    public float Current => (float)(timeOrigin + stopwatch.Elapsed.TotalSeconds * timeFactor);

    float timeFactor = 1;
    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            var elapsed = stopwatch.Elapsed.TotalSeconds;
            var previousTime = timeOrigin + elapsed * timeFactor;
            timeFactor = value;
            timeOrigin = (float)(previousTime - elapsed * timeFactor);
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
        timeOrigin = (float)(time - stopwatch.Elapsed.TotalSeconds * timeFactor);
        return true;
    }
}