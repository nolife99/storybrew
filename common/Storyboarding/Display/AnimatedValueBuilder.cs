namespace StorybrewCommon.Storyboarding.Display;

using System;
using Commands;
using CommandValues;

#pragma warning disable CS1591
public class AnimatedValueBuilder<TValue>(AnimatedValue<TValue> value) : IAnimatedValueBuilder where TValue : CommandValue
{
    CompositeCommand<TValue> composite;
    Func<ITypedCommand<TValue>, ITypedCommand<TValue>> decorate;
    public void Add(ICommand command) => Add(command as Command<TValue>);
    public void StartDisplayLoop(LoopCommand loop)
    {
        if (composite is not null) throw new InvalidOperationException("Cannot start loop: already inside a loop or trigger");

        decorate = command =>
        {
            if (loop.CommandsStartTime != 0)
                throw new InvalidOperationException(
                    $"Commands in a loop must start at 0ms, but start at {loop.CommandsStartTime}ms");

            return new LoopDecorator<TValue>(command, loop.StartTime, loop.CommandsDuration, loop.LoopCount);
        };

        composite = new();
    }
    public void StartDisplayTrigger(TriggerCommand triggerCommand)
    {
        if (composite is not null) throw new InvalidOperationException("Cannot start trigger: already inside a loop or trigger");

        decorate = command => new TriggerDecorator<TValue>(command);
        composite = new();
    }
    public void EndDisplayComposite()
    {
        if (composite is null) throw new InvalidOperationException("Cannot complete loop or trigger: Not inside one");
        if (composite.HasCommands) value.Add(decorate(composite));

        composite = null;
        decorate = null;
    }
    public void Add(Command<TValue> command)
    {
        if (command is null) return;
        (composite ?? value).Add(command);
    }
}