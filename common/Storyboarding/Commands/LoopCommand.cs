namespace StorybrewCommon.Storyboarding.Commands;

using StorybrewCommon.Storyboarding.Display;
using StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public readonly struct LoopCommand
{
    readonly CommandGroup group;

    internal LoopCommand(CommandGroup group, float startTime, int loopCount)
    {
        this.group = group;
        StartTime = startTime;
        LoopCount = loopCount;
    }

    public int Id => group.Id;
    public float StartTime { get; }
    public int LoopCount { get; }
    public bool IsActive => group.IsActive;

    public void End() => group.End();

    public bool Add(ICommand command) => group.Add(command);
    public void AddCommand(ICommand command, float offset = 0) => group.AddCommand(command, offset);

    public void AddCommand<TValue>(CommandTimeline<TValue>.Command command, float offset = 0)
        where TValue : struct, ICommandValue<TValue>
        => group.AddCommand(command, offset);
}
