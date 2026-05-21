namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;
using Display;

#pragma warning disable CS1591
public readonly struct TriggerCommand
{
    readonly CommandGroup group;

    internal TriggerCommand(CommandGroup group, string triggerName, float startTime, float endTime, int triggerGroup)
    {
        this.group = group;
        TriggerName = triggerName;
        StartTime = startTime;
        EndTime = endTime;
        Group = triggerGroup;
    }

    public int Id => group.Id;
    public string TriggerName { get; }
    public float StartTime { get; }
    public float EndTime { get; }
    public int Group { get; }
    public bool IsActive => group.IsActive;

    public void End() => group.End();

    public bool Add(ICommand command) => group.Add(command);
    public void AddCommand(ICommand command, float offset = 0) => group.AddCommand(command, offset);

    public void AddCommand<TValue>(CommandTimeline<TValue>.Command command, float offset = 0)
        where TValue : struct, ICommandValue<TValue>
        => group.AddCommand(command, offset);
}
