namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.IO;
using Animations;
using CommandValues;

public sealed class CommandTimeline<TValue> where TValue : struct, ICommandValue<TValue>
{
    readonly List<CommandChannel<TValue>> channels = [];

    CommandChannel<TValue> defaultChannel, currentChannel;
    ChannelKind currentKind;

    enum ChannelKind : byte
    {
        Default,
        Loop,
        Trigger
    }

    internal CommandTimeline() { }
    internal CommandTimeline(TValue defaultValue) => DefaultValue = defaultValue;
    public TValue DefaultValue { get; internal set; }

    public bool HasCommands => channels.Count != 0 || currentChannel is { Count: > 0 };
    public bool HasOverlap => channels.Exists(static channel => channel.HasOverlap) || currentChannel is { HasOverlap: true };

    public TValue StartValue
    {
        get
        {
            if (!tryGetFirstCommand(out var command)) return DefaultValue;
            return command.StartValue;
        }
    }

    public TValue EndValue
    {
        get
        {
            if (!tryGetLastCommand(out var command)) return DefaultValue;
            return command.EndValue;
        }
    }

    /// <summary>Commands in their stored form. Loop and trigger channels are preserved as group markers.</summary>
    public CommandEnumerable Commands
    {
        get => new(channels, false);
    }

    /// <summary>Commands with loop channels expanded into absolute commands. Trigger groups are preserved.</summary>
    public CommandEnumerable ExpandedCommands
    {
        get => new(channels, true);
    }

    internal ViewEnumerable ExpandedCommandViews
    {
        get => new(channels);
    }

    internal bool Add(CommandKind kind,
        OsbEasing easing,
        float startTime,
        float endTime,
        TValue startValue,
        TValue endValue,
        bool maintainValue = true)
    {
        if (currentChannel is null)
        {
            currentChannel = defaultChannel = new CommandChannel<TValue>();
            channels.Add(currentChannel);
            currentKind = ChannelKind.Default;
        }

        return currentChannel.Add(kind, easing, startTime, endTime, startValue, endValue, maintainValue);
    }

    internal void StartLoopGroup(int id)
    {
        if (currentKind is not ChannelKind.Default && currentChannel is not null) EndGroup();

        currentChannel = new CommandChannelLoop<TValue> { Id = id };
        currentKind = ChannelKind.Loop;
    }

    internal void StartTriggerGroup(int id, string triggerName, float startTime, float endTime, int group)
    {
        if (currentKind is not ChannelKind.Default && currentChannel is not null) EndGroup();

        currentChannel = new CommandChannelTrigger<TValue>
        {
            Id = id,
            TriggerName = triggerName,
            TriggerStartTime = startTime,
            TriggerEndTime = endTime,
            Group = group
        };
        currentKind = ChannelKind.Trigger;
    }

    internal void EndLoopGroup(float loopStartTime, int loopCount, float loopDuration, float childOffset)
    {
        if (currentChannel is not CommandChannelLoop<TValue> loop)
        {
            currentChannel = defaultChannel;
            currentKind = ChannelKind.Default;
            return;
        }

        if (loop.Count > 0)
        {
            if (childOffset != 0) loop.OffsetAll(childOffset);
            loop.LoopStartTime = loopStartTime;
            loop.LoopCount = loopCount;
            loop.LoopDuration = loopDuration;
            channels.Add(loop);
        }

        currentChannel = defaultChannel;
        currentKind = ChannelKind.Default;
    }

    internal void EndTriggerGroup()
    {
        if (currentChannel is CommandChannelTrigger<TValue> trigger && trigger.Count > 0) channels.Add(trigger);

        currentChannel = defaultChannel;
        currentKind = ChannelKind.Default;
    }

    internal void EndGroup()
    {
        switch (currentKind)
        {
            case ChannelKind.Loop:
                EndLoopGroup(0, 1, 0, 0);
                break;

            case ChannelKind.Trigger:
                EndTriggerGroup();
                break;
        }
    }

    public TValue ValueAtTime(float time)
    {
        if (channels.Count == 0) return DefaultValue;

        if (channels.Count == 1) return ValueAtChannel(channels[0], time, DefaultValue);

        return ValueAtTimeSlow(time);
    }

    static TValue ValueAtChannel(CommandChannel<TValue> channel, float time, TValue defaultValue)
        => channel switch
        {
            CommandChannelLoop<TValue> loop => loop.ValueAtTimeDirect(time, defaultValue),
            CommandChannelTrigger<TValue> trigger => trigger.ValueAtTimeDirect(time, defaultValue),
            _ => channel.ValueAtTimeDirect(time, defaultValue)
        };

    TValue ValueAtTimeSlow(float time)
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
                    if (channelState is ResultState.CommandInPresent || channelState is ResultState.CommandInPast &&
                        currentResult.IsBefore(channelResult))
                    {
                        currentResult = channelResult;
                        currentState = channelState;
                    }

                    break;
            }
        }

        return currentState is ResultState.NoCommand ? DefaultValue : currentResult.ValueAtTime(time);
    }

    public bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (channels.Count == 0)
        {
            result = default;
            return false;
        }

        if (channels.Count == 1) return channels[0].ResultAtTime(time, out result);

        var currentState = ResultState.NoCommand;
        result = default;

        foreach (var channel in channels)
        {
            if (!channel.ResultAtTime(time, out var channelResult)) continue;

            var channelState = ResultState.CommandInPresent;
            if (time < channelResult.StartTime) channelState = ResultState.CommandInFuture;
            else if (channelResult.EndTime < time) channelState = ResultState.CommandInPast;

            switch (currentState)
            {
                case ResultState.NoCommand:
                    result = channelResult;
                    currentState = channelState;
                    break;

                case ResultState.CommandInPresent:
                    if (channelState is ResultState.CommandInPresent && channelResult.IsBefore(result)) result = channelResult;
                    break;

                case ResultState.CommandInFuture:
                    if (channelState is not ResultState.CommandInFuture || channelResult.IsBefore(result))
                    {
                        result = channelResult;
                        currentState = channelState;
                    }

                    break;

                case ResultState.CommandInPast:
                    if (channelState is ResultState.CommandInPresent || channelState is ResultState.CommandInPast &&
                        result.IsBefore(channelResult))
                    {
                        result = channelResult;
                        currentState = channelState;
                    }

                    break;
            }
        }

        return currentState is not ResultState.NoCommand;
    }

    internal void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation)
    {
        foreach (var channel in channels) channel.WriteOsb(writer, exportSettings, in transform, indentation);
    }

    internal bool FindStartEdge(Func<TValue, bool> isZero, Func<TValue, TValue, bool> isNoOp, out float startEdge)
    {
        startEdge = float.MaxValue;

        if (!tryGetFirstMeaningfulCommand(isNoOp, out var command)) return false;
        if (!isZero(command.StartValue)) return false;

        startEdge = command.StartTime;
        return true;
    }

    internal bool FindEndEdge(Func<TValue, bool> isZero, Func<TValue, TValue, bool> isNoOp, out float endEdge)
    {
        endEdge = float.MinValue;

        if (!tryGetLastMeaningfulCommand(isNoOp, out var lastMeaningfulCommand)) return false;
        if (!isZero(lastMeaningfulCommand.EndValue)) return false;

        foreach (var command in ExpandedCommandViews)
        {
            if (isAfter(command, lastMeaningfulCommand)) continue;
            endEdge = float.Max(endEdge, command.EndTime);
        }

        return endEdge != float.MinValue;
    }

    static bool isAfter(CommandView<TValue> left, CommandView<TValue> right)
        => left.StartTime > right.StartTime || left.StartTime == right.StartTime && left.EndTime > right.EndTime;

    bool tryGetFirstMeaningfulCommand(Func<TValue, TValue, bool> isNoOp, out CommandView<TValue> result)
    {
        result = default;
        var hasResult = false;

        foreach (var command in ExpandedCommandViews)
        {
            if (isNoOp(command.StartValue, command.EndValue)) continue;

            if (!hasResult || isAfter(result, command))
            {
                result = command;
                hasResult = true;
            }
        }

        return hasResult;
    }

    bool tryGetLastMeaningfulCommand(Func<TValue, TValue, bool> isNoOp, out CommandView<TValue> result)
    {
        result = default;
        var hasResult = false;

        foreach (var command in ExpandedCommandViews)
        {
            if (isNoOp(command.StartValue, command.EndValue)) continue;

            if (!hasResult || isAfter(command, result))
            {
                result = command;
                hasResult = true;
            }
        }

        return hasResult;
    }

    bool tryGetFirstCommand(out CommandView<TValue> result)
    {
        result = default;
        var hasResult = false;

        foreach (var command in ExpandedCommandViews)
            if (!hasResult || isAfter(result, command))
            {
                result = command;
                hasResult = true;
            }

        return hasResult;
    }

    bool tryGetLastCommand(out CommandView<TValue> result)
    {
        result = default;
        var hasResult = false;

        foreach (var command in ExpandedCommandViews)
            if (!hasResult || command.EndTime > result.EndTime ||
                command.EndTime == result.EndTime && command.StartTime > result.StartTime)
            {
                result = command;
                hasResult = true;
            }

        return hasResult;
    }

    public enum CommandType : byte
    {
        Command,
        StartLoopGroup,
        StartTriggerGroup,
        EndGroup
    }

    public readonly struct Command
    {
        public readonly CommandType Type;
        public readonly CommandKind Kind;
        public readonly float StartTime, EndTime;
        public readonly float BaseStartTime, BaseEndTime, TimeOffset;
        public readonly OsbEasing Easing;
        public readonly TValue StartValue, EndValue;
        public readonly int Id, LoopCount, TriggerGroup;
        readonly string triggerName;

        public bool IsCommand => Type is CommandType.Command;
        public string TriggerName => triggerName ?? string.Empty;

        internal Command(CommandType type,
            CommandKind kind = default,
            float startTime = 0,
            float endTime = 0,
            float baseStartTime = 0,
            float baseEndTime = 0,
            float timeOffset = 0,
            OsbEasing easing = OsbEasing.None,
            TValue startValue = default,
            TValue endValue = default,
            int id = 0,
            int loopCount = 0,
            string triggerName = null,
            int triggerGroup = 0)
        {
            Type = type;
            Kind = kind;
            StartTime = startTime;
            EndTime = endTime;
            BaseStartTime = baseStartTime;
            BaseEndTime = baseEndTime;
            TimeOffset = timeOffset;
            Easing = easing;
            StartValue = startValue;
            EndValue = endValue;
            Id = id;
            LoopCount = loopCount;
            this.triggerName = triggerName;
            TriggerGroup = triggerGroup;
        }

        internal static Command FromView(CommandView<TValue> view)
            => new(CommandType.Command,
                view.Kind,
                view.StartTime,
                view.EndTime,
                view.BaseStartTime,
                view.BaseEndTime,
                view.TimeOffset,
                view.Easing,
                view.StartValue,
                view.EndValue);

        internal static Command StartLoop(int id, float startTime, int loopCount)
            => new(CommandType.StartLoopGroup, startTime: startTime, id: id, loopCount: loopCount);

        internal static Command StartTrigger(int id, string triggerName, float startTime, float endTime, int group)
            => new(CommandType.StartTriggerGroup,
                startTime: startTime,
                endTime: endTime,
                id: id,
                triggerName: triggerName,
                triggerGroup: group);

        internal static Command EndGroup(int id = 0) => new(CommandType.EndGroup, id: id);

        public TValue ValueAtTime(float time)
        {
            if (Type is not CommandType.Command) return default;

            var localTime = time - TimeOffset;
            var duration = BaseEndTime - BaseStartTime;
            return StartValue + (EndValue - StartValue) *
                (duration > 0 ? Easing.Ease((localTime - BaseStartTime) / duration) : 0);
        }
    }

    public readonly struct CommandEnumerable
    {
        readonly List<CommandChannel<TValue>> channels;
        readonly bool expanded;

        internal CommandEnumerable(List<CommandChannel<TValue>> channels, bool expanded)
        {
            this.channels = channels;
            this.expanded = expanded;
        }

        public CommandEnumerator GetEnumerator() => new(channels, expanded);
    }

    public struct CommandEnumerator
    {
        readonly List<CommandChannel<TValue>> channels;
        readonly bool expanded;
        int channelIndex, commandIndex, groupStage;

        internal CommandEnumerator(List<CommandChannel<TValue>> channels, bool expanded)
        {
            this.channels = channels;
            this.expanded = expanded;
            channelIndex = 0;
            commandIndex = -1;
            groupStage = 0;
            Current = default;
        }

        public Command Current { get; private set; }

        public bool MoveNext()
        {
            while (channelIndex < channels.Count)
            {
                var channel = channels[channelIndex];

                if (expanded && channel is not CommandChannelTrigger<TValue>)
                {
                    var next = commandIndex + 1;
                    if (next < channel.ViewCount)
                    {
                        commandIndex = next;
                        Current = Command.FromView(channel.ViewAt(next));
                        return true;
                    }

                    advanceChannel();
                    continue;
                }

                if (channel is CommandChannelLoop<TValue> loop)
                {
                    if (groupStage == 0)
                    {
                        groupStage = 1;
                        Current = Command.StartLoop(loop.Id, loop.LoopStartTime, loop.LoopCount);
                        return true;
                    }

                    var next = commandIndex + 1;
                    if (next < loop.Count)
                    {
                        commandIndex = next;
                        Current = Command.FromView(loop.GetView(next));
                        return true;
                    }

                    if (groupStage == 1)
                    {
                        groupStage = 2;
                        Current = Command.EndGroup(channel switch { CommandChannelLoop<TValue> l => l.Id, CommandChannelTrigger<TValue> t => t.Id, _ => 0 });
                        return true;
                    }

                    advanceChannel();
                    continue;
                }

                if (channel is CommandChannelTrigger<TValue> trigger)
                {
                    if (groupStage == 0)
                    {
                        groupStage = 1;
                        Current = Command.StartTrigger(trigger.Id, trigger.TriggerName,
                            trigger.TriggerStartTime,
                            trigger.TriggerEndTime,
                            trigger.Group);
                        return true;
                    }

                    var next = commandIndex + 1;
                    if (next < trigger.Count)
                    {
                        commandIndex = next;
                        Current = Command.FromView(trigger.GetView(next));
                        return true;
                    }

                    if (groupStage == 1)
                    {
                        groupStage = 2;
                        Current = Command.EndGroup(channel switch { CommandChannelLoop<TValue> l => l.Id, CommandChannelTrigger<TValue> t => t.Id, _ => 0 });
                        return true;
                    }

                    advanceChannel();
                    continue;
                }

                var defaultNext = commandIndex + 1;
                if (defaultNext < channel.Count)
                {
                    commandIndex = defaultNext;
                    Current = Command.FromView(channel.GetView(defaultNext));
                    return true;
                }

                advanceChannel();
            }

            return false;
        }

        void advanceChannel()
        {
            channelIndex++;
            commandIndex = -1;
            groupStage = 0;
        }
    }

    internal readonly struct ViewEnumerable
    {
        readonly List<CommandChannel<TValue>> channels;

        internal ViewEnumerable(List<CommandChannel<TValue>> channels) => this.channels = channels;

        public Enumerator GetEnumerator() => new(channels);
    }

    internal struct Enumerator
    {
        readonly List<CommandChannel<TValue>> channels;
        int channelIndex, viewIndex;

        internal Enumerator(List<CommandChannel<TValue>> channels)
        {
            this.channels = channels;
            channelIndex = 0;
            viewIndex = -1;
            Current = default;
        }

        public CommandView<TValue> Current { get; private set; }

        public bool MoveNext()
        {
            while (channelIndex < channels.Count)
            {
                var channel = channels[channelIndex];
                var nextView = viewIndex + 1;

                if (nextView < channel.ViewCount)
                {
                    viewIndex = nextView;
                    Current = channel.ViewAt(nextView);
                    return true;
                }

                channelIndex++;
                viewIndex = -1;
            }

            return false;
        }
    }

    enum ResultState
    {
        NoCommand, CommandInPresent, CommandInFuture, CommandInPast
    }
}
