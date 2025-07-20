namespace StorybrewCommon.Storyboarding.Display;

using CommandValues;

internal sealed class CommandChannelLoop<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue
{
    public int LoopCount = 1;
    public float LoopStartTime, LoopDuration;

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (Commands.Length == 0)
        {
            result = default;
            return false;
        }

        if (time < LoopStartTime)
        {
            result = Commands[0].AsResult(LoopStartTime);
            return true;
        }

        var loopTime = time - LoopStartTime;
        if (loopTime >= LoopCount * LoopDuration)
        {
            result = Commands[^1].AsResult(LoopStartTime + (LoopCount - 1) * LoopDuration);
            return true;
        }

        if (loopTime < LoopDuration)
        {
            result = CommandAtTime(loopTime).AsResult(LoopStartTime);
            return true;
        }

        var loopNumber = (int)(loopTime / LoopDuration);
        loopTime %= LoopDuration;

        if (loopTime <= Commands[0].StartTime)
        {
            result = Commands[^1].AsResult(LoopStartTime + (loopNumber - 1) * LoopDuration);
            return true;
        }

        result = CommandAtTime(loopTime).AsResult(LoopStartTime + loopNumber * LoopDuration);
        return true;
    }
}