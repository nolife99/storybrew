namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.Linq;
using Commands;
using CommandValues;
using ZLinq;

public interface CommandTimeline
{
    bool HasCommands { get; }
    bool HasOverlap { get; }

    void Add(ICommand command);
    void StartGroup(LoopCommand loop);
    void StartGroup(TriggerCommand trigger);
    void EndGroup();
}

public class CommandTimeline<TValue> : CommandTimeline where TValue : struct, ICommandValue
{
    readonly List<CommandChannel<TValue>> channels = [];

    CommandChannel<TValue> defaultChannel, currentChannel;
    public TValue DefaultValue;
    Action<CommandChannel<TValue>> groupEndAction;

    public CommandTimeline() { }
    public CommandTimeline(TValue defaultValue) => DefaultValue = defaultValue;

    public CommandResult<TValue> StartResult => HasCommands ?
        channels.AsValueEnumerable().Select(c => c.StartResult).MinBy(r => r.StartTime) :
        default;

    public CommandResult<TValue> EndResult
        => HasCommands ? channels.AsValueEnumerable().Select(c => c.EndResult).MaxBy(r => r.StartTime) : default;

    public TValue StartValue => HasCommands ?
        channels.AsValueEnumerable().Select(c => c.StartResult).MinBy(r => r.StartTime).StartValue :
        DefaultValue;

    public TValue EndValue => HasCommands ?
        channels.AsValueEnumerable().Select(c => c.EndResult).MaxBy(r => r.EndTime).EndValue :
        DefaultValue;

    public bool HasCommands => channels.Count > 0;
    public bool HasOverlap => channels.Any(c => c.HasOverlap);

    public void Add(ICommand command) => Add(command as Command<TValue>);

    public void StartGroup(LoopCommand loop)
    {
        if (groupEndAction is not null) EndGroup();

        CommandChannelLoop<TValue> loopChannel = new();
        currentChannel = loopChannel;

        groupEndAction = channel =>
        {
            var loopChannel = (CommandChannelLoop<TValue>)channel;

            loopChannel.LoopCount = loop.LoopCount;
            loopChannel.LoopStartTime = loop.StartTime;
            loopChannel.LoopDuration = loop.CommandsDuration;
        };
    }

    public void StartGroup(TriggerCommand trigger)
    {
        if (groupEndAction is not null) EndGroup();

        CommandChannelTrigger<TValue> triggerChannel = new();
        currentChannel = triggerChannel;
    }

    public void EndGroup()
    {
        if (groupEndAction is null) return;

        if (currentChannel.Commands.Count > 0)
        {
            groupEndAction(currentChannel);
            channels.Add(currentChannel);
        }

        currentChannel = defaultChannel;
        groupEndAction = null;
    }

    public void Add(ITypedCommand<TValue> command)
    {
        if (command is null) return;

        if (currentChannel is null) channels.Add(currentChannel = defaultChannel = new());

        currentChannel.Add(command);
    }

    public TValue ValueAtTime(float time)
    {
        var currentState = ResultState.NoCommand;
        CommandResult<TValue> currentResult = default;

        foreach (var channel in channels)
        {
            if (!channel.ResultAtTime(time, out var channelResult)) continue;

            var channelState = ResultState.CommandInPresent;
            if (time < channelResult.StartTime) channelState = ResultState.CommandInFuture;
            else if (channelResult.EndTime < time) channelState = ResultState.CommandInPast;

            switch (currentState)
            {
                case ResultState.NoCommand:
                    currentResult = channelResult;
                    currentState = channelState;
                    break;

                case ResultState.CommandInPresent:
                    if (channelState is ResultState.CommandInPresent && channelResult.IsBefore(currentResult))
                        currentResult = channelResult;

                    break;

                case ResultState.CommandInFuture:
                    if (channelState is not ResultState.CommandInFuture || channelResult.IsBefore(currentResult))
                    {
                        currentResult = channelResult;
                        currentState = channelState;
                    }

                    break;

                case ResultState.CommandInPast:
                    if (channelState is ResultState.CommandInPresent ||
                        channelState is ResultState.CommandInPast && currentResult.IsBefore(channelResult))
                    {
                        currentResult = channelResult;
                        currentState = channelState;
                    }

                    break;
            }
        }

        return currentState switch { ResultState.NoCommand => DefaultValue, _ => currentResult.ValueAtTime(time) };
    }

    enum ResultState
    {
        NoCommand, CommandInPresent, CommandInFuture, CommandInPast
    }
}