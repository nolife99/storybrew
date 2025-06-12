namespace StorybrewCommon.Storyboarding.Display;

using CommandValues;

internal class CommandChannelLoop<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue
{
    public int LoopCount = 1;
    public float LoopStartTime, LoopDuration;

    public override CommandResult<TValue> StartResult => StartCommand.AsResult(LoopStartTime);
    public override CommandResult<TValue> EndResult => EndCommand.AsResult(LoopStartTime + (LoopCount - 1) * LoopDuration);

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (Commands.Length == 0)
        {
            result = default;
            return false;
        }

        if (time < LoopStartTime)
        {
            result = StartResult;
            return true;
        }

        var loopTime = time - LoopStartTime;
        if (loopTime >= LoopCount * LoopDuration)
        {
            result = EndResult;
            return true;
        }

        if (loopTime < LoopDuration)
        {
            result = CommandAtTime(loopTime).AsResult(LoopStartTime);
            return true;
        }

        var loopNumber = (int)(loopTime / LoopDuration);
        loopTime %= LoopDuration;

        if (loopTime <= StartCommand.StartTime)
        {
            result = EndCommand.AsResult(LoopStartTime + (loopNumber - 1) * LoopDuration);
            return true;
        }

        result = CommandAtTime(loopTime).AsResult(LoopStartTime + loopNumber * LoopDuration);
        return true;
    }
}