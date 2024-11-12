namespace BrewLib.Time;

using System;

public interface FrameTimeSource : ReadOnlyTimeSource
{
    float Previous { get; }
    float Elapsed { get; }
}

public class FrameClock : FrameTimeSource
{
    public float Current { get; private set; }
    public float Previous { get; private set; }

    public float Elapsed => Current - Previous;
    public float TimeFactor => 1;

    public bool Playing => true;

    public event EventHandler Changed;

    public void AdvanceFrameTo(float time)
    {
        Previous = Current;
        Current = time;

        if (Previous != Current) Changed?.Invoke(this, EventArgs.Empty);
    }
}