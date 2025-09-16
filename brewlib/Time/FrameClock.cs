namespace BrewLib.Time;

using System;

public interface FrameTimeSource : ReadOnlyTimeSource
{
    TimeSpan Previous { get; }
    TimeSpan Elapsed { get; }
}

public class FrameClock : FrameTimeSource
{
    public TimeSpan Current { get; private set; }
    public TimeSpan Previous { get; private set; }

    public TimeSpan Elapsed => Current - Previous;
    public float TimeFactor => 1;

    public bool Playing => true;

    public event Action<FrameClock> Changed;

    public void AdvanceFrameTo(TimeSpan time)
    {
        Previous = Current;
        Current = time;

        if (Previous != Current) Changed?.Invoke(this);
    }
}