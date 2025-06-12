namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Commands;
using CommandValues;

public interface CommandTimeline
{
    bool HasCommands { get; }
    bool HasOverlap { get; }

    ReadOnlySpan<ICommand> Commands { get; }

    internal bool Add(ICommand command);
    internal void StartGroup(LoopCommand loop);
    internal void StartGroup(TriggerCommand trigger);
    internal void EndGroup();
}

public class CommandTimeline<TValue> : CommandTimeline where TValue : struct, ICommandValue
{
    List<CommandChannel<TValue>> channels;

    CommandChannel<TValue> defaultChannel, currentChannel;
    public TValue DefaultValue;
    Action<CommandChannel<TValue>> groupEndAction;

    public CommandTimeline() { }
    public CommandTimeline(TValue defaultValue) => DefaultValue = defaultValue;

    public CommandResult<TValue> StartResult
    {
        get
        {
            if (!HasCommands) return default;

            var earliestResult = channels[0].StartResult;
            foreach (var channel in channels)
            {
                var result = channel.StartResult;
                if (result.StartTime < earliestResult.StartTime) earliestResult = result;
            }

            return earliestResult;
        }
    }

    public CommandResult<TValue> EndResult
    {
        get
        {
            if (!HasCommands) return default;

            var latestResult = channels[0].EndResult;
            foreach (var channel in channels)
            {
                var result = channel.EndResult;
                if (result.StartTime > latestResult.StartTime) latestResult = result;
            }

            return latestResult;
        }
    }

    public TValue StartValue
    {
        get
        {
            if (!HasCommands) return DefaultValue;

            var earliestResult = channels[0].StartResult;
            foreach (var channel in channels)
            {
                var result = channel.StartResult;
                if (result.StartTime < earliestResult.StartTime) earliestResult = result;
            }

            return earliestResult.StartValue;
        }
    }

    public TValue EndValue
    {
        get
        {
            if (!HasCommands) return DefaultValue;

            var latestResult = channels[0].EndResult;
            foreach (var channel in channels)
            {
                var result = channel.EndResult;
                if (result.EndTime > latestResult.EndTime) latestResult = result;
            }

            return latestResult.EndValue;
        }
    }

    public ReadOnlySpan<ICommand> Commands => defaultChannel is null ?
        default :
        Unsafe.BitCast<ReadOnlySpan<ITypedCommand<TValue>>, ReadOnlySpan<ICommand>>(defaultChannel.Commands);

    public bool HasCommands => channels is not null && channels.Count > 0;

    public bool HasOverlap
    {
        get
        {
            if (!HasCommands) return false;

            foreach (var channel in channels)
                if (channel.HasOverlap)
                    return true;

            return false;
        }
    }

    bool CommandTimeline.Add(ICommand command) => Add(command as Command<TValue>);

    void CommandTimeline.StartGroup(LoopCommand loop)
    {
        if (groupEndAction is not null) ((CommandTimeline)this).EndGroup();

        CommandChannelLoop<TValue> loopChannel = new();
        currentChannel = loopChannel;

        groupEndAction = channel =>
        {
            var c = (CommandChannelLoop<TValue>)channel;

            c.LoopCount = loop.LoopCount;
            c.LoopStartTime = loop.StartTime;
            c.LoopDuration = loop.CommandsDuration;
        };
    }

    void CommandTimeline.StartGroup(TriggerCommand trigger)
    {
        if (groupEndAction is not null) ((CommandTimeline)this).EndGroup();

        currentChannel = new CommandChannelTrigger<TValue>();
    }

    void CommandTimeline.EndGroup()
    {
        if (groupEndAction is null) return;

        if (currentChannel.Commands.Length > 0)
        {
            groupEndAction(currentChannel);

            channels ??= [];
            channels.Add(currentChannel);
        }

        currentChannel = defaultChannel;
        groupEndAction = null;
    }

    bool Add(ITypedCommand<TValue> command)
    {
        if (command is null) return false;

        if (currentChannel is null)
        {
            channels ??= [];
            channels.Add(currentChannel = defaultChannel = new());
        }

        return currentChannel.Add(command);
    }

    public TValue ValueAtTime(float time)
    {
        if (!HasCommands) return DefaultValue;

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