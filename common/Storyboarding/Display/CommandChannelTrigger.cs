namespace StorybrewCommon.Storyboarding.Display;

using StorybrewCommon.Storyboarding.CommandValues;

sealed class CommandChannelTrigger<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
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

        result = new(CommandAtTime(time - TriggerTime), TriggerTime);
        return true;
    }
}