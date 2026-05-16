namespace StorybrewCommon.Storyboarding.Commands;

using StorybrewCommon.Storyboarding.Display;
using StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public readonly struct CommandGroup
{
    readonly OsbSprite sprite;
    readonly int id;

    public int Id => id;

    internal CommandGroup(OsbSprite sprite, int id)
    {
        this.sprite = sprite;
        this.id = id;
    }

    public bool IsActive => sprite is not null && sprite.IsCommandGroupActive(id);

    public void End()
    {
        EnsureActive();
        sprite.EndGroup();
    }

    public bool Add(ICommand command)
    {
        AddCommand(command);
        return true;
    }

    public void AddCommand(ICommand command, float offset = 0)
    {
        EnsureActive();
        sprite.AddCommandToGroup(id, command, offset);
    }

    public void AddCommand<TValue>(CommandTimeline<TValue>.Command command, float offset = 0)
        where TValue : struct, ICommandValue<TValue>
    {
        EnsureActive();
        sprite.AddCommandToGroup(id, command, offset);
    }

    public void EnsureActive()
    {
        if (!IsActive) throw new System.InvalidOperationException("This command group is no longer active.");
    }
}
