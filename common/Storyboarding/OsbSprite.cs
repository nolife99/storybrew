namespace StorybrewCommon.Storyboarding;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.Util;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;
using StorybrewCommon.Util;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using ZLinq;

///<summary> Base sprite in storyboards. </summary>
public class OsbSprite : StoryboardObject
{
    ///<summary> Default position of sprites, unless modified elsewhere. </summary>
    public static readonly CommandPosition DefaultPosition = new(320, 240);

    float commandsStartTime = float.MaxValue, commandsEndTime = float.MinValue, displayEndTime = float.MaxValue,
        displayStartTime = float.MinValue;

    CurrentGroupKind currentGroupKind;
    int currentGroupId, nextGroupId;
    float currentGroupStartTime, currentGroupEndTime, currentGroupCommandsStartTime, currentGroupCommandsEndTime;
    int currentGroupLoopCount, currentGroupTriggerGroup;
    string currentGroupTriggerName;

    enum CurrentGroupKind : byte
    {
        None, Loop, Trigger
    }

    CommandPosition initialPosition;

    ///<summary> Origin of this sprite. </summary>
    public OsbOrigin Origin = OsbOrigin.Centre;

    string texturePath = "";

    ///<summary> Constructs a new abstract sprite. </summary>
    protected OsbSprite() => InitialPosition = DefaultPosition;

    public bool HasTrigger { get; private set; }

    /// <summary> If the sprite has more commands than this amount, they will be split between multiple sprites. </summary>
    /// <remarks> Does not apply when the sprite has triggers. </remarks>
    public int CommandSplitThreshold { get; set; }

    ///<returns> True if the sprite is in a command group, else returns false. </returns>
    public bool InGroup => currentGroupKind is not CurrentGroupKind.None;

    /// <returns> The path to the image of the <see cref="OsbSprite"/>. </returns>
    public string TexturePath { get => texturePath; set => texturePath = PathHelper.WithStandardSeparators(value); }

    /// <returns> The initial position of the <see cref="OsbSprite"/>. </returns>
    public CommandPosition InitialPosition
    {
        get => initialPosition;
        set
        {
            if (initialPosition == value) return;

            initialPosition = value;
            MoveTimeline.DefaultValue = initialPosition;
            MoveXTimeline.DefaultValue = initialPosition.X;
            MoveYTimeline.DefaultValue = initialPosition.Y;
        }
    }

    /// <returns> The total amount of commands, including loops, being run on this instance of the <see cref="OsbSprite"/>. </returns>
    public int CommandCost { get; private set; }

    /// <returns> True if the <see cref="OsbSprite"/> has incompatible commands, else returns false. </returns>
    public bool HasIncompatibleCommands { get; private set; }

    /// <returns> True if the <see cref="OsbSprite"/> has overlapping commands, else returns false. </returns>
    public bool HasOverlappedCommands { get; private set; }

    public bool HasMoveCommands { get; private set; }
    public bool HasScalingCommands { get; private set; }

    ///<summary> Gets the start time of the first command on this sprite. </summary>
    public override float StartTime
    {
        get
        {
            if (commandsStartTime == float.MaxValue) refreshStartEndTimes();
            return commandsStartTime;
        }
    }

    ///<summary> Gets the end time of the last command on this sprite. </summary>
    public override float EndTime
    {
        get
        {
            if (commandsEndTime == float.MinValue) refreshStartEndTimes();
            return commandsEndTime;
        }
    }

    /// <returns> Image of the sprite at <paramref name="time"/>. </returns>
    public virtual string GetTexturePathAt(float time) => texturePath;

    void refreshStartEndTimes()
    {
        clearStartEndTimes();
        includeTimeline(MoveTimeline);
        includeTimeline(MoveXTimeline);
        includeTimeline(MoveYTimeline);
        includeTimeline(ScaleTimeline);
        includeTimeline(ScaleVecTimeline);
        includeTimeline(RotateTimeline);
        includeTimeline(FadeTimeline);
        includeTimeline(ColorTimeline);
        includeTimeline(AdditiveTimeline);
        includeTimeline(FlipHTimeline);
        includeTimeline(FlipVTimeline);

        void includeTimeline<TValue>(CommandTimeline<TValue> timeline)
            where TValue : struct, ICommandValue<TValue>
        {
            foreach (var command in timeline.ExpandedCommandViews) includeCommandTimes(command.StartTime, command.EndTime);
        }

        if (!HasTrigger)
        {
            if (FadeTimeline.HasCommands)
            {
                Func<CommandDecimal, bool> isZero = static value => value == 0;
                Func<CommandDecimal, CommandDecimal, bool> isNoOp = static (startValue, endValue)
                    => startValue == .0 && endValue == .0;

                if (FadeTimeline.FindStartEdge(isZero, isNoOp, out var startEdge))
                    displayStartTime = float.Max(displayStartTime, startEdge);

                if (FadeTimeline.FindEndEdge(isZero, isNoOp, out var endEdge))
                    displayEndTime = float.Min(displayEndTime, endEdge);
            }

            if (ScaleTimeline.HasCommands)
            {
                Func<CommandDecimal, bool> isZero = value => value == 0;
                Func<CommandDecimal, CommandDecimal, bool> isNoOp = (startValue, endValue)
                    => startValue == 0 && endValue == 0;

                if (ScaleTimeline.FindStartEdge(isZero, isNoOp, out var startEdge))
                    displayStartTime = float.Max(displayStartTime, startEdge);

                if (ScaleTimeline.FindEndEdge(isZero, isNoOp, out var endEdge))
                    displayEndTime = float.Min(displayEndTime, endEdge);
            }

            if (ScaleVecTimeline.HasCommands)
            {
                Func<CommandScale, bool> isZero = value => value == default;
                Func<CommandScale, CommandScale, bool> isNoOp = (startValue, endValue)
                    => startValue == default && endValue == default;

                if (ScaleVecTimeline.FindStartEdge(isZero, isNoOp, out var startEdge))
                    displayStartTime = float.Max(displayStartTime, startEdge);

                if (ScaleVecTimeline.FindEndEdge(isZero, isNoOp, out var endEdge))
                    displayEndTime = float.Min(displayEndTime, endEdge);
            }
        }

        displayStartTime = float.Max(displayStartTime, commandsStartTime);
        displayEndTime = float.Min(displayEndTime, commandsEndTime);
    }

    void clearStartEndTimes()
    {
        commandsStartTime = float.MaxValue;
        commandsEndTime = float.MinValue;
        displayStartTime = float.MinValue;
        displayEndTime = float.MaxValue;
    }

    void includeCommandTimes(float startTime, float endTime)
    {
        commandsStartTime = float.Min(commandsStartTime, startTime);
        commandsEndTime = float.Max(commandsEndTime, endTime);
    }

    //==========M==========//
    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endPosition"> End <see cref="CommandPosition"/> value of the command. </param>
    public void Move(OsbEasing easing,
        float startTime,
        float endTime,
        CommandPosition startPosition,
        CommandPosition endPosition)
        => addCommand(CommandKind.Move, MoveTimeline, easing, startTime, endTime, startPosition, endPosition);

    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(OsbEasing easing,
        float startTime,
        float endTime,
        CommandPosition startPosition,
        double endX,
        double endY)
        => Move(easing, startTime, endTime, startPosition, new(endX, endY));

    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="startY"> Start-Y value of the command. </param>
    /// <param name="endPosition"> End <see cref="CommandPosition"/> value of the command. </param>
    public void Move(OsbEasing easing,
        float startTime,
        float endTime,
        double startX,
        double startY,
        CommandPosition endPosition)
        => Move(easing, startTime, endTime, new(startX, startY), endPosition);

    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="startY"> Start-Y value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(OsbEasing easing,
        float startTime,
        float endTime,
        double startX,
        double startY,
        double endX,
        double endY)
        => Move(easing, startTime, endTime, new(startX, startY), new(endX, endY));

    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endPosition"> End <see cref="CommandPosition"/> value of the command. </param>
    public void Move(float startTime, float endTime, CommandPosition startPosition, CommandPosition endPosition)
        => Move(OsbEasing.None, startTime, endTime, startPosition, endPosition);

    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(float startTime, float endTime, CommandPosition startPosition, double endX, double endY)
        => Move(OsbEasing.None, startTime, endTime, startPosition, endX, endY);

    /// <summary> Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="startY"> Start-Y value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(float startTime, float endTime, double startX, double startY, double endX, double endY)
        => Move(OsbEasing.None, startTime, endTime, startX, startY, endX, endY);

    /// <summary> Sets the position of an <see cref="OsbSprite"/>. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="position"> <see cref="CommandPosition"/> value of the command. </param>
    public void Move(float time, CommandPosition position) => Move(OsbEasing.None, time, time, position, position);

    /// <summary> Sets the position of an <see cref="OsbSprite"/>. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="x"> X value of the command. </param>
    /// <param name="y"> Y value of the command. </param>
    public void Move(float time, double x, double y) => Move(OsbEasing.None, time, time, x, y, x, y);

    //==========MX==========//
    /// <summary> Change the x-position of a <see cref="OsbSprite"/> over time. Commands are also available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    public void MoveX(OsbEasing easing, float startTime, float endTime, double startX, double endX)
        => addCommand(CommandKind.MoveX, MoveXTimeline, easing, startTime, endTime, (CommandDecimal)startX, (CommandDecimal)endX);

    /// <summary> Change the x-position of a <see cref="OsbSprite"/> over time. Commands are also available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    public void MoveX(float startTime, float endTime, double startX, double endX)
        => MoveX(OsbEasing.None, startTime, endTime, startX, endX);

    /// <summary> Sets the X-Position of an <see cref="OsbSprite"/>. Commands are also available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="x"> X value of the command. </param>
    public void MoveX(float time, double x) => MoveX(OsbEasing.None, time, time, x, x);

    //==========MY==========//
    /// <summary> Change the Y-Position of an <see cref="OsbSprite"/> over time. Commands are also available for MoveX. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startY"> Start-Y value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void MoveY(OsbEasing easing, float startTime, float endTime, double startY, double endY)
        => addCommand(CommandKind.MoveY, MoveYTimeline, easing, startTime, endTime, (CommandDecimal)startY, (CommandDecimal)endY);

    /// <summary> Change the Y-Position of an <see cref="OsbSprite"/> over time. Commands are also available for MoveX. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startY"> Start-Y value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void MoveY(float startTime, float endTime, double startY, double endY)
        => MoveY(OsbEasing.None, startTime, endTime, startY, endY);

    /// <summary> Sets the Y-Position of an <see cref="OsbSprite"/>. Commands are also available for MoveX. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="y"> Y value of the command. </param>
    public void MoveY(float time, float y) => MoveY(OsbEasing.None, time, time, y, y);

    //==========S==========//
    /// <summary> Change the size of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="VScaleCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start scale of the command. </param>
    /// <param name="endScale"> End scale of the command. </param>
    public void Scale(OsbEasing easing, float startTime, float endTime, double startScale, double endScale)
        => addCommand(CommandKind.Scale, ScaleTimeline, easing, startTime, endTime, (CommandDecimal)startScale, (CommandDecimal)endScale);

    /// <summary> Change the size of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="VScaleCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start scale of the command. </param>
    /// <param name="endScale"> End scale of the command. </param>
    public void Scale(float startTime, float endTime, double startScale, double endScale)
        => Scale(OsbEasing.None, startTime, endTime, startScale, endScale);

    /// <summary> Sets the size of a sprite. </summary>
    /// <remarks> Cannot be used with <see cref="VScaleCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="scale"> Scale of the command. </param>
    public void Scale(float time, double scale) => Scale(OsbEasing.None, time, time, scale, scale);

    //==========V==========//
    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start <see cref="CommandScale"/> value of the command. </param>
    /// <param name="endScale"> End <see cref="CommandScale"/> value of the command. </param>
    public void ScaleVec(OsbEasing easing,
        float startTime,
        float endTime,
        CommandScale startScale,
        CommandScale endScale)
        => addCommand(CommandKind.ScaleVec, ScaleVecTimeline, easing, startTime, endTime, startScale, endScale);

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start <see cref="CommandScale"/> value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(OsbEasing easing,
        float startTime,
        float endTime,
        CommandScale startScale,
        double endX,
        double endY)
        => ScaleVec(easing, startTime, endTime, startScale, new(endX, endY));

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start X-Scale value of the command. </param>
    /// <param name="startY"> Start Y-Scale value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(OsbEasing easing,
        float startTime,
        float endTime,
        double startX,
        double startY,
        double endX,
        double endY)
        => ScaleVec(easing, startTime, endTime, new(startX, startY), new(endX, endY));

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start <see cref="CommandScale"/> value of the command. </param>
    /// <param name="endScale"> End <see cref="CommandScale"/> value of the command. </param>
    public void ScaleVec(float startTime, float endTime, CommandScale startScale, CommandScale endScale)
        => ScaleVec(OsbEasing.None, startTime, endTime, startScale, endScale);

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start <see cref="CommandScale"/> value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(float startTime, float endTime, CommandScale startScale, double endX, double endY)
        => ScaleVec(OsbEasing.None, startTime, endTime, startScale, endX, endY);

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start X-Scale value of the command. </param>
    /// <param name="startY"> Start Y-Scale value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(float startTime, float endTime, double startX, double startY, double endX, double endY)
        => ScaleVec(OsbEasing.None, startTime, endTime, startX, startY, endX, endY);

    /// <summary> Sets the vector scale of a sprite. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="scale"> <see cref="CommandScale"/> value of the command. </param>
    public void ScaleVec(float time, CommandScale scale) => ScaleVec(OsbEasing.None, time, time, scale, scale);

    /// <summary> Sets the vector scale of a sprite. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="x"> Scale-X value of the command. </param>
    /// <param name="y"> Scale-Y value of the command. </param>
    public void ScaleVec(float time, double x, double y) => ScaleVec(OsbEasing.None, time, time, x, y, x, y);

    //==========R==========//
    /// <summary> Change the rotation of an <see cref="OsbSprite"/> over time. Angles are in radians. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startRotation"> Start radians of the command. </param>
    /// <param name="endRotation"> End radians of the command. </param>
    public void Rotate(OsbEasing easing, float startTime, float endTime, double startRotation, double endRotation)
        => addCommand(CommandKind.Rotate, RotateTimeline, easing, startTime, endTime, (CommandDecimal)startRotation, (CommandDecimal)endRotation);

    /// <summary> Change the rotation of an <see cref="OsbSprite"/> over time. Angles are in radians. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startRotation"> Start radians of the command. </param>
    /// <param name="endRotation"> End radians of the command. </param>
    public void Rotate(float startTime, float endTime, double startRotation, double endRotation)
        => Rotate(OsbEasing.None, startTime, endTime, startRotation, endRotation);

    /// <summary> Sets the rotation of an <see cref="OsbSprite"/>. Angles are in radians. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="rotation"> Radians of the command. </param>
    public void Rotate(float time, double rotation) => Rotate(OsbEasing.None, time, time, rotation, rotation);

    //==========F==========//
    /// <summary> Change the opacity of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startFade"> Start fade value of the command. </param>
    /// <param name="endFade"> End fade value of the command. </param>
    public void Fade(OsbEasing easing, float startTime, float endTime, double startFade, double endFade)
        => addCommand(CommandKind.Fade, FadeTimeline, easing, startTime, endTime, (CommandDecimal)startFade, (CommandDecimal)endFade);

    /// <summary> Change the opacity of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startFade"> Start fade value of the command. </param>
    /// <param name="endFade"> End fade value of the command. </param>
    public void Fade(float startTime, float endTime, double startFade, double endFade)
        => Fade(OsbEasing.None, startTime, endTime, startFade, endFade);

    /// <summary> Sets the opacity of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="fade"> Fade value of the command. </param>
    public void Fade(float time, double fade) => Fade(OsbEasing.None, time, time, fade, fade);

    //==========C==========//
    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endColor"> End <see cref="CommandColor"/> value of the command. </param>
    public void Color(OsbEasing easing, float startTime, float endTime, CommandColor startColor, CommandColor endColor)
        => addCommand(CommandKind.Color, ColorTimeline, easing, startTime, endTime, startColor, endColor);

    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endR"> End red value of the command. </param>
    /// <param name="endG"> End green value of the command. </param>
    /// <param name="endB"> End blue value of the command. </param>
    public void Color(OsbEasing easing,
        float startTime,
        float endTime,
        CommandColor startColor,
        double endR,
        double endG,
        double endB)
        => Color(easing, startTime, endTime, startColor, new(endR, endG, endB));

    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startR"> Start red value of the command. </param>
    /// <param name="startG"> Start green value of the command. </param>
    /// <param name="startB"> Start blue value of the command. </param>
    /// <param name="endR"> End red value of the command. </param>
    /// <param name="endG"> End green value of the command. </param>
    /// <param name="endB"> End blue value of the command. </param>
    public void Color(OsbEasing easing,
        float startTime,
        float endTime,
        double startR,
        double startG,
        double startB,
        double endR,
        double endG,
        double endB)
        => Color(easing, startTime, endTime, new(startR, startG, startB), new(endR, endG, endB));

    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endColor"> End <see cref="CommandColor"/> value of the command. </param>
    public void Color(float startTime, float endTime, CommandColor startColor, CommandColor endColor)
        => Color(OsbEasing.None, startTime, endTime, startColor, endColor);

    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endR"> End red value of the command. </param>
    /// <param name="endG"> End green value of the command. </param>
    /// <param name="endB"> End blue value of the command. </param>
    public void Color(float startTime, float endTime, CommandColor startColor, double endR, double endG, double endB)
        => Color(OsbEasing.None, startTime, endTime, startColor, endR, endG, endB);

    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startR"> Start red value of the command. </param>
    /// <param name="startG"> Start green value of the command. </param>
    /// <param name="startB"> Start blue value of the command. </param>
    /// <param name="endR"> End red value of the command. </param>
    /// <param name="endG"> End green value of the command. </param>
    /// <param name="endB"> End blue value of the command. </param>
    public void Color(float startTime,
        float endTime,
        double startR,
        double startG,
        double startB,
        double endR,
        double endG,
        double endB)
        => Color(OsbEasing.None, startTime, endTime, startR, startG, startB, endR, endG, endB);

    /// <summary> Sets the RGB color of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="color"> The <see cref="CommandColor"/> value of the command. </param>
    public void Color(float time, CommandColor color) => Color(OsbEasing.None, time, time, color, color);

    /// <summary> Sets the RGB color of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="r"> Red value of the command. </param>
    /// <param name="g"> Green value of the command. </param>
    /// <param name="b"> Blue value of the command. </param>
    public void Color(float time, double r, double g, double b) => Color(OsbEasing.None, time, time, r, g, b, r, g, b);

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endH"> End hue value (in degrees) of the command. </param>
    /// <param name="endS"> End saturation value (from 0 to 1) of the command. </param>
    /// <param name="endB"> End brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(OsbEasing easing,
        float startTime,
        float endTime,
        CommandColor startColor,
        double endH,
        double endS,
        double endB)
        => Color(easing, startTime, endTime, startColor, CommandColor.FromHsb(endH, endS, endB));

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startH"> Start hue value (in degrees) of the command. </param>
    /// <param name="startS"> Start saturation value (from 0 to 1) of the command. </param>
    /// <param name="startB"> Start brightness level (from 0 to 1) of the command. </param>
    /// <param name="endColor"> End <see cref="CommandColor"/> value of the command. </param>
    public void ColorHsb(OsbEasing easing,
        float startTime,
        float endTime,
        double startH,
        double startS,
        double startB,
        CommandColor endColor)
        => Color(easing, startTime, endTime, CommandColor.FromHsb(startH, startS, startB), endColor);

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startH"> Start hue value (in degrees) of the command. </param>
    /// <param name="startS"> Start saturation value (from 0 to 1) of the command. </param>
    /// <param name="startB"> Start brightness level (from 0 to 1) of the command. </param>
    /// <param name="endH"> End hue value (in degrees) of the command. </param>
    /// <param name="endS"> End saturation value (from 0 to 1) of the command. </param>
    /// <param name="endB"> End brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(OsbEasing easing,
        float startTime,
        float endTime,
        double startH,
        double startS,
        double startB,
        double endH,
        double endS,
        double endB)
        => Color(easing,
            startTime,
            endTime,
            CommandColor.FromHsb(startH, startS, startB),
            CommandColor.FromHsb(endH, endS, endB));

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startH"> Start hue value (in degrees) of the command. </param>
    /// <param name="startS"> Start saturation value (from 0 to 1) of the command. </param>
    /// <param name="startB"> Start brightness level (from 0 to 1) of the command. </param>
    /// <param name="endColor"> End <see cref="CommandColor"/> value of the command. </param>
    public void ColorHsb(float startTime,
        float endTime,
        double startH,
        double startS,
        double startB,
        CommandColor endColor)
        => ColorHsb(OsbEasing.None, startTime, endTime, startH, startS, startB, endColor);

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endH"> End hue value (in degrees) of the command. </param>
    /// <param name="endS"> End saturation value (from 0 to 1) of the command. </param>
    /// <param name="endB"> End brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(float startTime, float endTime, CommandColor startColor, double endH, double endS, double endB)
        => ColorHsb(OsbEasing.None, startTime, endTime, startColor, endH, endS, endB);

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startH"> Start hue value (in degrees) of the command. </param>
    /// <param name="startS"> Start saturation value (from 0 to 1) of the command. </param>
    /// <param name="startB"> Start brightness level (from 0 to 1) of the command. </param>
    /// <param name="endH"> End hue value (in degrees) of the command. </param>
    /// <param name="endS"> End saturation value (from 0 to 1) of the command. </param>
    /// <param name="endB"> End brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(float startTime,
        float endTime,
        double startH,
        double startS,
        double startB,
        double endH,
        double endS,
        double endB)
        => ColorHsb(OsbEasing.None, startTime, endTime, startH, startS, startB, endH, endS, endB);

    /// <summary> Sets the hue, saturation, and brightness of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="h"> Hue value (in degrees) of the command. </param>
    /// <param name="s"> Saturation value (from 0 to 1) of the command. </param>
    /// <param name="b"> Brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(float time, double h, double s, double b)
        => ColorHsb(OsbEasing.None, time, time, h, s, b, h, s, b);

    //==========P==========//
    /// <summary> Apply a parameter to an <see cref="OsbSprite"/> for a given duration. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="param"> The <see cref="CommandParameter"/> type to be applied. </param>
    public void Parameter(float startTime, float endTime, CommandParameter param)
        => addParameterCommand(startTime, endTime, param);

    /// <summary> Flip an <see cref="OsbSprite"/> horizontally for a given duration. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    public void FlipH(float startTime, float endTime) => Parameter(startTime, endTime, CommandParameter.FlipHorizontal);

    /// <summary> Flips an <see cref="OsbSprite"/> horizontally. </summary>
    /// <param name="time"> Time of the command. </param>
    public void FlipH(float time) => FlipH(time, time);

    /// <summary> Flip an <see cref="OsbSprite"/> vertically for a given duration. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    public void FlipV(float startTime, float endTime) => Parameter(startTime, endTime, CommandParameter.FlipVertical);

    /// <summary> Flips an <see cref="OsbSprite"/> horizontally. </summary>
    /// <param name="time"> Time of the command. </param>
    public void FlipV(float time) => FlipV(time, time);

    /// <summary> Apply additive blending to an <see cref="OsbSprite"/> for a given duration. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    public void Additive(float startTime, float endTime)
        => Parameter(startTime, endTime, CommandParameter.AdditiveBlending);

    /// <summary> Applies additive blending to an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    public void Additive(float time) => Additive(time, time);

    /// <summary> Repeat commands <paramref name="loopCount"/> times until <see cref="EndGroup"/> is called. </summary>
    /// <remarks> Command times inside the loop are relative to the <paramref name="startTime"/> of the loop. </remarks>
    /// <param name="startTime"> Start time of the loop. </param>
    /// <param name="loopCount"> How many times the loop should repeat. </param>
    public LoopCommand StartLoopGroup(float startTime, int loopCount)
        => StartLoopGroup(startTime, loopCount, 0);

    internal LoopCommand StartLoopGroup(float startTime, int loopCount, int id)
    {
        if (InGroup) EndGroup();

        currentGroupId = id != 0 ? id : ++nextGroupId;
        if (id > nextGroupId) nextGroupId = id;
        if (currentGroupId == 0) currentGroupId = ++nextGroupId;

        currentGroupKind = CurrentGroupKind.Loop;
        currentGroupStartTime = startTime;
        currentGroupEndTime = 0;
        currentGroupLoopCount = loopCount;
        currentGroupCommandsStartTime = float.MaxValue;
        currentGroupCommandsEndTime = float.MinValue;

        MoveTimeline.StartLoopGroup(currentGroupId);
        MoveXTimeline.StartLoopGroup(currentGroupId);
        MoveYTimeline.StartLoopGroup(currentGroupId);
        ScaleTimeline.StartLoopGroup(currentGroupId);
        ScaleVecTimeline.StartLoopGroup(currentGroupId);
        RotateTimeline.StartLoopGroup(currentGroupId);
        FadeTimeline.StartLoopGroup(currentGroupId);
        ColorTimeline.StartLoopGroup(currentGroupId);
        AdditiveTimeline.StartLoopGroup(currentGroupId);
        FlipHTimeline.StartLoopGroup(currentGroupId);
        FlipVTimeline.StartLoopGroup(currentGroupId);

        return new(new CommandGroup(this, currentGroupId), startTime, loopCount);
    }

    /// <summary>
    /// Commands on the <see cref="OsbSprite"/> until <see cref="EndGroup"/> is called will be active when the
    /// <paramref name="triggerName"/> event happens until <paramref name="endTime"/>.
    /// </summary>
    /// <remarks> Command times inside the loop are relative to the <paramref name="startTime"/> of the trigger loop. </remarks>
    /// <param name="triggerName"> Trigger type of the loop </param>
    /// <param name="startTime"> Start time of the loop. </param>
    /// <param name="endTime"> End time of the loop. </param>
    /// <param name="group"> Group number of the loop. </param>
    public TriggerCommand StartTriggerGroup(string triggerName, float startTime, float endTime, int group = 0)
        => StartTriggerGroup(triggerName, startTime, endTime, group, 0);

    internal TriggerCommand StartTriggerGroup(string triggerName, float startTime, float endTime, int group, int id)
    {
        if (InGroup) EndGroup();

        currentGroupId = id != 0 ? id : ++nextGroupId;
        if (id > nextGroupId) nextGroupId = id;
        if (currentGroupId == 0) currentGroupId = ++nextGroupId;

        currentGroupKind = CurrentGroupKind.Trigger;
        currentGroupStartTime = startTime;
        currentGroupEndTime = endTime;
        currentGroupLoopCount = 1;
        currentGroupTriggerName = triggerName;
        currentGroupTriggerGroup = group;
        currentGroupCommandsStartTime = float.MaxValue;
        currentGroupCommandsEndTime = float.MinValue;

        MoveTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        MoveXTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        MoveYTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        ScaleTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        ScaleVecTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        RotateTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        FadeTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        ColorTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        AdditiveTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        FlipHTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);
        FlipVTimeline.StartTriggerGroup(currentGroupId, triggerName, startTime, endTime, group);

        HasTrigger = true;
        return new(new CommandGroup(this, currentGroupId), triggerName, startTime, endTime, group);
    }

    ///<summary> Calls the end of a loop. </summary>
    public void EndGroup()
    {
        switch (currentGroupKind)
        {
            case CurrentGroupKind.Loop:
            {
                var commandsStart = currentGroupCommandsStartTime == float.MaxValue ? 0 : currentGroupCommandsStartTime;
                var commandsEnd = currentGroupCommandsEndTime == float.MinValue ? 0 : currentGroupCommandsEndTime;
                var loopStart = currentGroupStartTime + commandsStart;
                var loopDuration = commandsEnd - commandsStart;
                var childOffset = -commandsStart;

                MoveTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                MoveXTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                MoveYTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                ScaleTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                ScaleVecTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                RotateTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                FadeTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                ColorTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                AdditiveTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                FlipHTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);
                FlipVTimeline.EndLoopGroup(loopStart, currentGroupLoopCount, loopDuration, childOffset);

                break;
            }

            case CurrentGroupKind.Trigger:
                MoveTimeline.EndTriggerGroup();
                MoveXTimeline.EndTriggerGroup();
                MoveYTimeline.EndTriggerGroup();
                ScaleTimeline.EndTriggerGroup();
                ScaleVecTimeline.EndTriggerGroup();
                RotateTimeline.EndTriggerGroup();
                FadeTimeline.EndTriggerGroup();
                ColorTimeline.EndTriggerGroup();
                AdditiveTimeline.EndTriggerGroup();
                FlipHTimeline.EndTriggerGroup();
                FlipVTimeline.EndTriggerGroup();
                break;
        }

        currentGroupKind = CurrentGroupKind.None;
        currentGroupId = 0;
    }

    void addParameterCommand(float startTime, float endTime, CommandParameter param)
    {
        switch (param.Type)
        {
            case ParameterType.AdditiveBlending:
                addCommand(CommandKind.Additive, AdditiveTimeline, OsbEasing.None, startTime, endTime, param, param);
                break;
            case ParameterType.FlipHorizontal:
                addCommand(CommandKind.FlipH, FlipHTimeline, OsbEasing.None, startTime, endTime, param, param);
                break;
            case ParameterType.FlipVertical:
                addCommand(CommandKind.FlipV, FlipVTimeline, OsbEasing.None, startTime, endTime, param, param);
                break;
        }
    }

    void addCommand<TValue>(CommandKind kind,
        CommandTimeline<TValue> timeline,
        OsbEasing easing,
        float startTime,
        float endTime,
        TValue startValue,
        TValue endValue) where TValue : struct, ICommandValue<TValue>
    {
        if (timeline.Add(kind, easing, startTime, endTime, startValue, endValue)) ++CommandCost;

        if (currentGroupKind is not CurrentGroupKind.None)
        {
            currentGroupCommandsStartTime = float.Min(currentGroupCommandsStartTime, startTime);
            currentGroupCommandsEndTime = float.Max(currentGroupCommandsEndTime, startTime > endTime ? startTime : endTime);
        }

        afterCommandAdded();
    }

    void afterCommandAdded()
    {
        clearStartEndTimes();

        HasOverlappedCommands = MoveTimeline.HasOverlap || MoveXTimeline.HasOverlap || MoveYTimeline.HasOverlap ||
            ScaleTimeline.HasOverlap || ScaleVecTimeline.HasOverlap || RotateTimeline.HasOverlap ||
            FadeTimeline.HasOverlap || ColorTimeline.HasOverlap || AdditiveTimeline.HasOverlap ||
            FlipHTimeline.HasOverlap || FlipVTimeline.HasOverlap;

        HasIncompatibleCommands =
            MoveTimeline.HasCommands && (MoveXTimeline.HasCommands || MoveYTimeline.HasCommands) ||
            ScaleTimeline.HasCommands && ScaleVecTimeline.HasCommands;

        HasMoveCommands = MoveXTimeline.HasCommands || MoveYTimeline.HasCommands || MoveTimeline.HasCommands;
        HasScalingCommands = ScaleTimeline.HasCommands || ScaleVecTimeline.HasCommands;
    }

    /// <summary> Adds a command to be run on the sprite. </summary>
    /// <param name="command"> The command type to be run. </param>
    /// <param name="offset"> </param>
    public void AddCommand(ICommand command, float offset = 0)
    {
        switch (command)
        {
            case ColorCommand color:
                Color(color.Easing, color.startTime + offset, color.endTime + offset, color.StartValue, color.EndValue);
                break;

            case FadeCommand fade:
                Fade(fade.Easing, fade.startTime + offset, fade.endTime + offset, fade.StartValue, fade.EndValue);
                break;

            case ScaleCommand scale:
                Scale(scale.Easing, scale.startTime + offset, scale.endTime + offset, scale.StartValue, scale.EndValue);
                break;

            case VScaleCommand vScale:
                ScaleVec(vScale.Easing,
                    vScale.startTime + offset,
                    vScale.endTime + offset,
                    vScale.StartValue,
                    vScale.EndValue);

                break;

            case ParameterCommand param:
                Parameter(param.startTime + offset, param.endTime + offset, param.StartValue);
                break;

            case MoveCommand move:
                Move(move.Easing, move.startTime + offset, move.endTime + offset, move.StartValue, move.EndValue);
                break;

            case MoveXCommand moveX:
                MoveX(moveX.Easing, moveX.startTime + offset, moveX.endTime + offset, moveX.StartValue, moveX.EndValue);
                break;

            case MoveYCommand moveY:
                MoveY(moveY.Easing, moveY.startTime + offset, moveY.endTime + offset, moveY.StartValue, moveY.EndValue);
                break;

            case RotateCommand rotate:
                Rotate(rotate.Easing,
                    rotate.startTime + offset,
                    rotate.endTime + offset,
                    rotate.StartValue,
                    rotate.EndValue);

                break;

            default:
                throw new NotSupportedException(
                    $"Failed to add command: No support for adding command of type {command.GetType().FullName}");
        }
    }

    public void AddCommand<TValue>(CommandTimeline<TValue>.Command command, float offset = 0)
        where TValue : struct, ICommandValue<TValue>
    {
        if (tryAddGroupCommand(command, offset)) return;

        var startTime = command.StartTime + offset;
        var endTime = command.EndTime + offset;

        switch (command.Kind)
        {
            case CommandKind.Move:
                addCommand(CommandKind.Move,
                    MoveTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandPosition>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandPosition>(command.EndValue));
                break;

            case CommandKind.MoveX:
                addCommand(CommandKind.MoveX,
                    MoveXTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.EndValue));
                break;

            case CommandKind.MoveY:
                addCommand(CommandKind.MoveY,
                    MoveYTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.EndValue));
                break;

            case CommandKind.Scale:
                addCommand(CommandKind.Scale,
                    ScaleTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.EndValue));
                break;

            case CommandKind.ScaleVec:
                addCommand(CommandKind.ScaleVec,
                    ScaleVecTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandScale>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandScale>(command.EndValue));
                break;

            case CommandKind.Rotate:
                addCommand(CommandKind.Rotate,
                    RotateTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.EndValue));
                break;

            case CommandKind.Fade:
                addCommand(CommandKind.Fade,
                    FadeTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandDecimal>(command.EndValue));
                break;

            case CommandKind.Color:
                addCommand(CommandKind.Color,
                    ColorTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandColor>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandColor>(command.EndValue));
                break;

            case CommandKind.Additive:
                addCommand(CommandKind.Additive,
                    AdditiveTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandParameter>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandParameter>(command.EndValue));
                break;

            case CommandKind.FlipH:
                addCommand(CommandKind.FlipH,
                    FlipHTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandParameter>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandParameter>(command.EndValue));
                break;

            case CommandKind.FlipV:
                addCommand(CommandKind.FlipV,
                    FlipVTimeline,
                    command.Easing,
                    startTime,
                    endTime,
                    CommandChannel<TValue>.commandValue<TValue, CommandParameter>(command.StartValue),
                    CommandChannel<TValue>.commandValue<TValue, CommandParameter>(command.EndValue));
                break;

            default:
                throw new NotSupportedException(command.Kind.ToString());
        }
    }

    bool tryAddGroupCommand<TValue>(CommandTimeline<TValue>.Command command, float offset)
        where TValue : struct, ICommandValue<TValue>
    {
        switch (command.Type)
        {
            case CommandTimeline<TValue>.CommandType.Command:
                return false;

            case CommandTimeline<TValue>.CommandType.StartLoopGroup:
                StartLoopGroup(command.StartTime + offset, command.LoopCount, command.Id);
                return true;

            case CommandTimeline<TValue>.CommandType.StartTriggerGroup:
                StartTriggerGroup(command.TriggerName,
                    command.StartTime + offset,
                    command.EndTime + offset,
                    command.TriggerGroup,
                    command.Id);
                return true;

            case CommandTimeline<TValue>.CommandType.EndGroup:
                EndGroup();
                return true;

            default:
                throw new NotSupportedException(command.Type.ToString());
        }
    }

    internal bool IsCommandGroupActive(int groupId)
        => groupId != 0 && currentGroupId == groupId && currentGroupKind is not CurrentGroupKind.None;

    internal void AddCommandToGroup(int groupId, ICommand command, float offset = 0)
    {
        ensureCommandGroupActive(groupId);
        AddCommand(command, offset);
    }

    internal void AddCommandToGroup<TValue>(
        int groupId,
        CommandTimeline<TValue>.Command command,
        float offset = 0)
        where TValue : struct, ICommandValue<TValue>
    {
        ensureCommandGroupActive(groupId);
        AddCommand(command, offset);
    }

    void ensureCommandGroupActive(int groupId)
    {
        if (!IsCommandGroupActive(groupId))
            throw new InvalidOperationException("This command group is no longer active.");
    }

    /// <returns> True if the sprite is active at <paramref name="time"/>, else returns false. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsActive(float time) => commandsStartTime <= time && time <= commandsEndTime;

    /// <returns> True if the sprite is visible at <paramref name="time"/>, else returns false. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InDisplayInterval(float time) => displayStartTime <= time && time <= displayEndTime;

    ///<summary> Writes this sprite's data to a stream. </summary>
    public override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        scoped ref readonly StoryboardTransform transform)
    {
        if (CommandCost == 0) return;

        WriteHeader(writer, exportSettings, layer, transform);
        MoveTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        MoveXTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        MoveYTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        ScaleTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        ScaleVecTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        RotateTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        FadeTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        ColorTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        AdditiveTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        FlipHTimeline.WriteOsb(writer, exportSettings, in transform, 1);
        FlipVTimeline.WriteOsb(writer, exportSettings, in transform, 1);
    }

    private protected virtual void WriteHeader(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        StoryboardTransform transform)
    {
        writer.Write("Sprite,");
        WriteHeaderCommon(writer, exportSettings, layer, transform);
        writer.WriteLine();
    }

    private protected void WriteHeaderCommon(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        StoryboardTransform transform)
    {
        var transformedInitialPosition = transform.IsIdentity ? InitialPosition :
            MoveXTimeline.HasCommands || MoveYTimeline.HasCommands ?
                (CommandPosition)transform.ApplyToPositionXY(InitialPosition) :
                transform.ApplyToPosition(InitialPosition);

        writer.Write(Enum.GetName(layer));
        writer.Write(',');
        writer.Write(Enum.GetName(Origin));
        writer.Write(",\"");
        writer.Write(texturePath.AsSpan().Trim());
        writer.Write("\",");

        writeDecimal(writer,
            exportSettings,
            !MoveTimeline.HasCommands && !MoveXTimeline.HasCommands ? transformedInitialPosition.X : 0);
        writer.Write(',');
        writeDecimal(writer,
            exportSettings,
            !MoveTimeline.HasCommands && !MoveYTimeline.HasCommands ? transformedInitialPosition.Y : 0);
    }

    static void writeDecimal(TextWriter writer, ExportSettings exportSettings, CommandDecimal value)
    {
        using var text = value.ToOsbString(exportSettings);
        writer.Write(text.AsReadOnlySpan());
    }

    /// <summary> Returns whether the sprite is within widescreen storyboard bounds. </summary>
    /// <param name="position"> The storyboard position, in osu!pixels, of the sprite. </param>
    /// <param name="size"> The image dimensions of the sprite texture. </param>
    /// <param name="rotation"> The rotation, in radians, of the sprite. </param>
    /// <param name="origin"> The <see cref="OsbOrigin"/> of the sprite. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InScreenBounds(CommandPosition position,
        CommandScale size,
        CommandDecimal rotation,
        OsbOrigin origin)
        => new OrientedBoundingBox(position, GetOriginVector(origin, size), size, rotation).Intersects(OsuHitObject
            .WidescreenStoryboardBounds);

    /// <summary> Gets the origin of a sprite based on its <see cref="OsbOrigin"/> </summary>
    /// <param name="origin"> The <see cref="OsbOrigin"/> to be taken into account. </param>
    /// <param name="size"> The size of the sprite. </param>
    public static Vector2 GetOriginVector(OsbOrigin origin, Vector2 size)
        => origin switch
        {
            OsbOrigin.TopLeft => Vector2.Zero,
            OsbOrigin.TopCentre => new(size.X * .5f, 0),
            OsbOrigin.TopRight => new(size.X, 0),
            OsbOrigin.CentreLeft => new(0, size.Y * .5f),
            OsbOrigin.Centre => size * .5f,
            OsbOrigin.CentreRight => new(size.X, size.Y * .5f),
            OsbOrigin.BottomLeft => new(0, size.Y),
            OsbOrigin.BottomCentre => new(size.X * .5f, size.Y),
            OsbOrigin.BottomRight => size,
            _ => throw new NotSupportedException(Enum.GetName(origin))
        };

    #region Display

    public readonly CommandTimeline<CommandPosition> MoveTimeline = new();

    public readonly CommandTimeline<CommandDecimal> MoveXTimeline = new(), MoveYTimeline = new(),
        ScaleTimeline = new(1), RotateTimeline = new(), FadeTimeline = new(1);

    public readonly CommandTimeline<CommandScale> ScaleVecTimeline = new(Vector2.One);
    public readonly CommandTimeline<CommandColor> ColorTimeline = new(CommandColor.White);

    public readonly CommandTimeline<CommandParameter> AdditiveTimeline = new(CommandParameter.None),
        FlipHTimeline = new(CommandParameter.None), FlipVTimeline = new(CommandParameter.None);

    /// <summary> Retrieves the <see cref="CommandPosition"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandPosition PositionAt(float time)
    {
        if (MoveTimeline.HasCommands) return MoveTimeline.ValueAtTime(time);
        if (!MoveXTimeline.HasCommands && !MoveYTimeline.HasCommands) return InitialPosition;

        return new(MoveXTimeline.HasCommands ? MoveXTimeline.ValueAtTime(time) : InitialPosition.X,
            MoveYTimeline.HasCommands ? MoveYTimeline.ValueAtTime(time) : InitialPosition.Y);
    }

    /// <summary> Retrieves the <see cref="CommandScale"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandScale ScaleAt(float time)
    {
        if (ScaleVecTimeline.HasCommands) return ScaleVecTimeline.ValueAtTime(time);
        return ScaleTimeline.HasCommands ? new(ScaleTimeline.ValueAtTime(time)) : Vector2.One;
    }

    /// <summary> Retrieves the rotation, in radians, of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandDecimal RotationAt(float time) => RotateTimeline.ValueAtTime(time);

    /// <summary> Retrieves the opacity level of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandDecimal OpacityAt(float time) => FadeTimeline.ValueAtTime(time);

    /// <summary> Retrieves the <see cref="CommandColor"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandColor ColorAt(float time) => ColorTimeline.ValueAtTime(time);

    /// <summary> Retrieves the additive value of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandParameter AdditiveAt(float time) => AdditiveTimeline.ValueAtTime(time);

    /// <summary> Retrieves the horizontal flip of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandParameter FlipHAt(float time) => FlipHTimeline.ValueAtTime(time);

    /// <summary> Retrieves the vertical flip of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandParameter FlipVAt(float time) => FlipVTimeline.ValueAtTime(time);


    #endregion
}
#pragma warning disable CS1591
public enum OsbLayer
{
    Background, Fail, Pass, Foreground, Overlay
}

///<summary> Enumeration values determining the origin of a sprite/image. </summary>
public enum OsbOrigin : byte
{
    ///<summary> The sprite is anchored at the top left of the image. </summary>
    TopLeft,

    ///<summary> The sprite is anchored at the center top of the image. </summary>
    TopCentre,

    ///<summary> The sprite is anchored at the top right of the image. </summary>
    TopRight,

    ///<summary> The sprite is anchored at the left center of the image. </summary>
    CentreLeft,

    ///<summary> The sprite is anchored at the center of the image. </summary>
    Centre,

    ///<summary> The sprite is anchored at the right center of the image. </summary>
    CentreRight,

    ///<summary> The sprite is anchored at the bottom left of the image. </summary>
    BottomLeft,

    ///<summary> The sprite is anchored at the bottom center of the image. </summary>
    BottomCentre,

    ///<summary> The sprite is anchored at the bottom right of the image. </summary>
    BottomRight
}

/// <summary> Apply an easing to a command. </summary>
/// <remarks> Visit <see href="http://easings.net/"/> for more information. </remarks>
public enum OsbEasing : byte
{
    None,
    Out,
    In,
    InQuad,
    OutQuad,
    InOutQuad,
    InCubic,
    OutCubic,
    InOutCubic,
    InQuart,
    OutQuart,
    InOutQuart,
    InQuint,
    OutQuint,
    InOutQuint,
    InSine,
    OutSine,
    InOutSine,
    InExpo,
    OutExpo,
    InOutExpo,
    InCirc,
    OutCirc,
    InOutCirc,
    InElastic,
    OutElastic,
    OutElasticHalf,
    OutElasticQuarter,
    InOutElastic,
    InBack,
    OutBack,
    InOutBack,
    InBounce,
    OutBounce,
    InOutBounce
}

///<summary> Define the loop type for an animation. </summary>
public enum OsbLoopType : byte
{
    ///<summary> Loops the animation frames for the sprite's lifetime, repeating when the last frame is reached. </summary>
    LoopForever,

    ///<summary> Loops the animation frames for the sprite once, stopping at the last frame. </summary>
    LoopOnce
}

///<summary> Define the parameter type for a parameter command. </summary>
public enum ParameterType : byte
{
    ///<exception cref="InvalidOperationException"> Do not pass this value to any parameter. </exception>
    None,

    ///<summary> Reflects the sprite across its center horizontally. </summary>
    FlipHorizontal,

    ///<summary> Reflects the sprite across its center vertically. </summary>
    FlipVertical,

    ///<summary> Applies additive blending to the sprite. </summary>
    AdditiveBlending
}