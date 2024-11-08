namespace BrewLib.Time;

using System.Diagnostics;

public class Clock : TimeSource
{
    readonly Stopwatch stopwatch = new();

    bool playing;
    float timeFactor = 1, timeOrigin;

    public float Current => timeOrigin + (float)stopwatch.Elapsed.TotalSeconds * timeFactor;
    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            var elapsed = (float)stopwatch.Elapsed.TotalSeconds;
            timeFactor = value;
            timeOrigin = timeOrigin + elapsed * timeFactor - elapsed * timeFactor;
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
        timeOrigin = time - (float)stopwatch.Elapsed.TotalSeconds * timeFactor;
        return true;
    }
}