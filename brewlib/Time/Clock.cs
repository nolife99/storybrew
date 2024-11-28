namespace BrewLib.Time;

using OpenTK.Windowing.GraphicsLibraryFramework;

public class Clock : TimeSource
{
    bool playing;
    float timeFactor = 1, timeOrigin, pausedTime;

    public float Current => timeOrigin + (playing ? (float)GLFW.GetTime() : pausedTime) * timeFactor;

    public float TimeFactor
    {
        get => timeFactor;
        set
        {
            if (timeFactor == value) return;

            var elapsed = playing ? (float)GLFW.GetTime() : pausedTime;
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

            if (playing) Seek(Current);
            else pausedTime = (float)GLFW.GetTime();
        }
    }

    public bool Seek(float time)
    {
        timeOrigin = time - (playing ? (float)GLFW.GetTime() : pausedTime) * timeFactor;
        return true;
    }
}