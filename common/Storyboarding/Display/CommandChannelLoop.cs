namespace StorybrewCommon.Storyboarding.Display;

using StorybrewCommon.Storyboarding.CommandValues;

sealed class CommandChannelLoop<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    public int LoopCount = 1;
    public float LoopStartTime, LoopDuration;

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        var c = commands;
        if (c.Count == 0)
        {
            result = default;
            return false;
        }

        var startTime = LoopStartTime;
        if (time < startTime)
        {
            result = new(c[0], startTime);
            return true;
        }

        var loopTime = time - startTime;
        var loopDuration = LoopDuration;

        if (loopTime >= LoopCount * loopDuration)
        {
            result = new(c[^1], startTime + (LoopCount - 1) * loopDuration);
            return true;
        }

        if (loopTime < loopDuration)
        {
            result = new(CommandAtTime(loopTime), startTime);
            return true;
        }

        var loopNumber = (int)(loopTime / loopDuration);
        loopTime %= loopDuration;

        if (loopTime <= c[0].StartTime)
        {
            result = new(c[^1], startTime + (loopNumber - 1) * loopDuration);
            return true;
        }

        result = new(CommandAtTime(loopTime), startTime + loopNumber * loopDuration);
        return true;
    }
}