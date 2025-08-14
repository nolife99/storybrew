namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.Linq;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary;

public interface ICommandTimeline
{
    bool HasCommands { get; }
    bool HasOverlap { get; }

    IEnumerable<ICommand> Commands { get; }

    internal bool Add(ICommand command);
    internal void StartGroup(LoopCommand loop);
    internal void StartGroup(TriggerCommand trigger);
    internal void EndGroup();
}

public sealed class CommandTimeline<TValue> : ICommandTimeline where TValue : struct, ICommandValue
{
    List<CommandChannel<TValue>> channels;

    CommandChannel<TValue> defaultChannel, currentChannel;
    Action<CommandChannel<TValue>, object> groupEndAction;
    object groupEndActionState;

    internal CommandTimeline() { }
    internal CommandTimeline(TValue defaultValue) => DefaultValue = defaultValue;
    public TValue DefaultValue { get; internal set; }

    public IEnumerable<ICommand> Commands => defaultChannel is null ? [] : defaultChannel.Commands.Select(c => c.Command);

    public bool HasCommands => channels is not null && channels.Count > 0;

    public bool HasOverlap => HasCommands && channels.Any(channel => channel.HasOverlap);

    bool ICommandTimeline.Add(ICommand command) => Add(command as Command<TValue>);

    void ICommandTimeline.StartGroup(LoopCommand loop)
    {
        if (groupEndAction is not null) ((ICommandTimeline)this).EndGroup();

        CommandChannelLoop<TValue> loopChannel = new();
        currentChannel = loopChannel;

        groupEndActionState = loop;
        groupEndAction = (channel, state) =>
        {
            var c = (CommandChannelLoop<TValue>)channel;
            var l = (LoopCommand)state;

            c.LoopCount = l.LoopCount;
            c.LoopStartTime = l.StartTime;
            c.LoopDuration = l.CommandsDuration;
        };
    }

    void ICommandTimeline.StartGroup(TriggerCommand trigger)
    {
        if (groupEndAction is not null) ((ICommandTimeline)this).EndGroup();

        currentChannel = new CommandChannelTrigger<TValue>();
    }

    void ICommandTimeline.EndGroup()
    {
        if (groupEndAction is null) return;

        if (currentChannel.Commands.Count > 0)
        {
            groupEndAction(currentChannel, groupEndActionState);

            channels ??= [];
            channels.Add(currentChannel);
        }

        currentChannel = defaultChannel;

        groupEndAction = null;
        groupEndActionState = null;
    }

    bool Add(Command<TValue> command)
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

    internal bool FindStartEdge(Func<TValue, bool> isZero, Func<TValue, TValue, bool> isNoOp, out float startEdge)
    {
        startEdge = float.MaxValue;

        using var results = getCommandResults();
        foreach (var result in results)
        {
            if (isNoOp(result.StartValue, result.EndValue)) continue;

            if (isZero(result.StartValue))
            {
                startEdge = float.Min(startEdge, result.StartTime);
                return true;
            }

            break;
        }

        return false;
    }

    internal bool FindEndEdge(Func<TValue, bool> isZero, Func<TValue, TValue, bool> isNoOp, out float endEdge)
    {
        endEdge = float.MinValue;

        using var results = getCommandResults();
        for (var i = results.Count - 1; i >= 0; --i)
        {
            var result = results[i];
            if (endEdge == float.MinValue)
            {
                if (isNoOp(result.StartValue, result.EndValue)) continue;

                if (isZero(result.EndValue)) endEdge = float.Max(endEdge, result.EndTime);
                else return false;
            }
            else endEdge = float.Max(endEdge, result.EndTime);
        }

        return endEdge != float.MinValue;
    }

    TempList<CommandResult<TValue>> getCommandResults()
    {
        var result = TempList.Create<CommandResult<TValue>>();
        foreach (var channel in channels)
            switch (channel)
            {
                case CommandChannelTrigger<TValue> trigger:
                    if (!trigger.Active) continue;

                    var commands = channel.Commands;
                    for (var i = 0; i < commands.Count; i++) result.Add(commands[i].WithOffset(trigger.TriggerTime));

                    break;

                case CommandChannelLoop<TValue> loop:
                    for (var loopIndex = 0; loopIndex < loop.LoopCount; ++loopIndex)
                    {
                        commands = channel.Commands;
                        for (var i = 0; i < commands.Count; i++)
                            result.Add(commands[i].WithOffset(loop.LoopStartTime + loopIndex * loop.LoopDuration));
                    }

                    break;

                default: result.AddRange(channel.Commands); break;
            }

        result.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
        return result;
    }

    enum ResultState
    {
        NoCommand, CommandInPresent, CommandInFuture, CommandInPast
    }
}