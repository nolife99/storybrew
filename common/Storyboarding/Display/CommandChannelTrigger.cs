namespace StorybrewCommon.Storyboarding.Display;

using CommandValues;

internal sealed class CommandChannelTrigger<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue
{
    public bool Active;
    public float TriggerTime;

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