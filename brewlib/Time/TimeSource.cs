namespace BrewLib.Time;

using System;

public interface ReadOnlyTimeSource
{
    TimeSpan Current { get; }
    float TimeFactor { get; }

    bool Playing { get; }
}

public interface TimeSource : ReadOnlyTimeSource
{
    new float TimeFactor { get; set; }
    new bool Playing { get; set; }

    bool Seek(TimeSpan time);
}