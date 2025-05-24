namespace StorybrewCommon.Storyboarding.Display;

using CommandValues;

internal class CommandChannelTrigger<TValue> : CommandChannel<TValue> where TValue : struct, CommandValue
{
    public bool Active;
    public float TriggerTime;

    public override CommandResult<TValue> StartResult => StartCommand.AsResult(TriggerTime);
    public override CommandResult<TValue> EndResult => EndCommand.AsResult(TriggerTime);

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (!Active)
        {
            result = default;
            return false;
        }

        result = CommandAtTime(time - TriggerTime).AsResult(TriggerTime);
        return true;
    }
}