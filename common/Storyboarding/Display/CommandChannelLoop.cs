namespace StorybrewCommon.Storyboarding.Display;

using StorybrewCommon.Storyboarding.CommandValues;

sealed class CommandChannelLoop<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue
{
    public int LoopCount = 1;
    public float LoopStartTime, LoopDuration;

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (commands.Count == 0)
        {
            result = default;
            return false;
        }

        if (time < LoopStartTime)
        {
            result = commands[0].WithOffset(LoopStartTime);
            return true;
        }

        var loopTime = time - LoopStartTime;
        if (loopTime >= LoopCount * LoopDuration)
        {
            result = commands[^1].WithOffset(LoopStartTime + (LoopCount - 1) * LoopDuration);
            return true;
        }

        if (loopTime < LoopDuration)
        {
            result = CommandAtTime(loopTime).AsResult(LoopStartTime);
            return true;
        }

        var loopNumber = (int)(loopTime / LoopDuration);
        loopTime %= LoopDuration;

        if (loopTime <= commands[0].StartTime)
        {
            result = commands[^1].WithOffset(LoopStartTime + (loopNumber - 1) * LoopDuration);
            return true;
        }

        result = CommandAtTime(loopTime).AsResult(LoopStartTime + loopNumber * LoopDuration);
        return true;
    }
}