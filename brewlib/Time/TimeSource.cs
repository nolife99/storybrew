namespace BrewLib.Time;

public interface ReadOnlyTimeSource
{
    float Current { get; }
    float TimeFactor { get; }

    bool Playing { get; }
}

public interface TimeSource : ReadOnlyTimeSource
{
    new float TimeFactor { get; set; }
    new bool Playing { get; set; }

    bool Seek(float time);
}