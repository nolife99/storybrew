namespace StorybrewCommon.Storyboarding;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using BrewLib.Util;
using Commands;
using CommandValues;
using Display;
using Mapset;
using StorybrewCommon.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using ZLinq;

///<summary> Base sprite in storyboards. </summary>
public class OsbSprite : StoryboardObject
{
    ///<summary> Default position of sprites, unless modified elsewhere. </summary>
    public static readonly CommandPosition DefaultPosition = new(320, 240);

    internal readonly List<ICommand> commandGroups = [];

    float commandsStartTime = float.MaxValue, commandsEndTime = float.MinValue, displayEndTime = float.MaxValue,
        displayStartTime = float.MinValue;

    CommandGroup currentCommandGroup;

    CommandPosition initialPosition;

    ///<summary> Origin of this sprite. </summary>
    public OsbOrigin Origin = OsbOrigin.Centre;

    string texturePath = "";

    ///<summary> Constructs a new abstract sprite. </summary>
    protected OsbSprite()
    {
        displayValueBuilders =
        [
            (c => c is MoveCommand, MoveTimeline),
            (c => c is MoveXCommand, MoveXTimeline),
            (c => c is MoveYCommand, MoveYTimeline),
            (c => c is ScaleCommand, ScaleTimeline),
            (c => c is VScaleCommand, ScaleVecTimeline),
            (c => c is RotateCommand, RotateTimeline),
            (c => c is FadeCommand, FadeTimeline),
            (c => c is ColorCommand, ColorTimeline),
            (c => c is ParameterCommand { StartValue.Type: ParameterType.AdditiveBlending }, AdditiveTimeline),
            (c => c is ParameterCommand { StartValue.Type: ParameterType.FlipHorizontal }, FlipHTimeline),
            (c => c is ParameterCommand { StartValue.Type: ParameterType.FlipVertical }, FlipVTimeline)
        ];

        InitialPosition = DefaultPosition;
    }

    public bool HasTrigger { get; private set; }

    /// <summary> If the sprite has more commands than this amount, they will be split between multiple sprites. </summary>
    /// <remarks> Does not apply when the sprite has triggers. </remarks>
    public int CommandSplitThreshold { get; set; }

    ///<returns> True if the sprite is in a command group, else returns false. </returns>
    public bool InGroup => currentCommandGroup is not null;

    /// <returns> The path to the image of the <see cref="OsbSprite"/>. </returns>
    public string TexturePath
    {
        get => texturePath;
        set
        {
            PathHelper.WithStandardSeparatorsUnsafe(value.AsSpan());
            texturePath = value;
        }
    }

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

    public float DisplayStartTime
    {
        get
        {
            if (displayStartTime == float.MinValue) refreshStartEndTimes();
            return displayStartTime;
        }
    }

    public float DisplayEndTime
    {
        get
        {
            if (displayEndTime == float.MaxValue) refreshStartEndTimes();
            return displayEndTime;
        }
    }

    /// <returns> Image of the sprite at <paramref name="time"/>. </returns>
    public virtual string GetTexturePathAt(float time) => texturePath;

    void refreshStartEndTimes()
    {
        clearStartEndTimes();
        foreach (var command in commandGroups.AsValueEnumerable()
            .Concat(displayValueBuilders.AsValueEnumerable().SelectMany(c => c.Item2.Commands.AsValueEnumerable())))
        {
            commandsStartTime = float.Min(commandsStartTime, command.StartTime);
            commandsEndTime = float.Max(commandsEndTime, command.EndTime);
        }

        if (!HasTrigger)
        {
            if (FadeTimeline.HasCommands)
            {
                var start = FadeTimeline.StartResult;
                if (start.StartValue == 0) displayStartTime = float.Max(displayStartTime, start.StartTime);

                var end = FadeTimeline.EndResult;
                if (end.EndValue == 0) displayEndTime = float.Min(displayEndTime, end.EndTime);
            }

            if (ScaleTimeline.HasCommands)
            {
                var start = ScaleTimeline.StartResult;
                if (start.StartValue == 0) displayStartTime = float.Max(displayStartTime, start.StartTime);

                var end = ScaleTimeline.EndResult;
                if (end.EndValue == 0) displayEndTime = float.Min(displayEndTime, end.EndTime);
            }

            if (ScaleVecTimeline.HasCommands)
            {
                var start = ScaleVecTimeline.StartResult;
                if (start.StartValue.X <= 0 || start.StartValue.Y <= 0)
                    displayStartTime = float.Max(displayStartTime, start.StartTime);

                var end = ScaleVecTimeline.EndResult;
                if (end.EndValue.X <= 0 || end.EndValue.Y <= 0) displayEndTime = float.Min(displayEndTime, end.EndTime);
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

    //==========M==========//
    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
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
        CommandPosition endPosition) => addCommand(new MoveCommand(easing, startTime, endTime, startPosition, endPosition));

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
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
        double endY) => Move(easing, startTime, endTime, startPosition, new(endX, endY));

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
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
        CommandPosition endPosition) => Move(easing, startTime, endTime, new(startX, startY), endPosition);

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
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
        double endY) => Move(easing, startTime, endTime, new(startX, startY), new(endX, endY));

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endPosition"> End <see cref="CommandPosition"/> value of the command. </param>
    public void Move(float startTime, float endTime, CommandPosition startPosition, CommandPosition endPosition)
        => Move(OsbEasing.None, startTime, endTime, startPosition, endPosition);

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(float startTime, float endTime, CommandPosition startPosition, double endX, double endY)
        => Move(OsbEasing.None, startTime, endTime, startPosition, endX, endY);

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
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
    /// <summary> Change the x-position of a <see cref="OsbSprite"/> over time. Commands are also available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    public void MoveX(OsbEasing easing, float startTime, float endTime, double startX, double endX)
        => addCommand(new MoveXCommand(easing, startTime, endTime, startX, endX));

    /// <summary> Change the x-position of a <see cref="OsbSprite"/> over time. Commands are also available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    public void MoveX(float startTime, float endTime, double startX, double endX)
        => MoveX(OsbEasing.None, startTime, endTime, startX, endX);

    /// <summary> Sets the X-Position of an <see cref="OsbSprite"/>. Commands are also available for MoveY.</summary>
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
        => addCommand(new MoveYCommand(easing, startTime, endTime, startY, endY));

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
        => addCommand(new ScaleCommand(easing, startTime, endTime, startScale, endScale));

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
    public void ScaleVec(OsbEasing easing, float startTime, float endTime, CommandScale startScale, CommandScale endScale)
        => addCommand(new VScaleCommand(easing, startTime, endTime, startScale, endScale));

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="easing"> <see cref="OsbEasing"/> to be applied to the command. </param>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start <see cref="CommandScale"/> value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(OsbEasing easing, float startTime, float endTime, CommandScale startScale, double endX, double endY)
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
        double endY) => ScaleVec(easing, startTime, endTime, new(startX, startY), new(endX, endY));

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
        => addCommand(new RotateCommand(easing, startTime, endTime, startRotation, endRotation));

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
        => addCommand(new FadeCommand(easing, startTime, endTime, startFade, endFade));

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
        => addCommand(new ColorCommand(easing, startTime, endTime, startColor, endColor));

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
        double endB) => Color(easing, startTime, endTime, startColor, new(endR, endG, endB));

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
        double endB) => Color(easing, startTime, endTime, new(startR, startG, startB), new(endR, endG, endB));

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
        double endB) => Color(OsbEasing.None, startTime, endTime, startR, startG, startB, endR, endG, endB);

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
        double endB) => Color(easing, startTime, endTime, startColor, CommandColor.FromHsb(endH, endS, endB));

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
        CommandColor endColor) => Color(easing, startTime, endTime, CommandColor.FromHsb(startH, startS, startB), endColor);

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
        double endB) => Color(easing,
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
    public void ColorHsb(float startTime, float endTime, double startH, double startS, double startB, CommandColor endColor)
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
        double endB) => ColorHsb(OsbEasing.None, startTime, endTime, startH, startS, startB, endH, endS, endB);

    /// <summary> Sets the hue, saturation, and brightness of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="h"> Hue value (in degrees) of the command. </param>
    /// <param name="s"> Saturation value (from 0 to 1) of the command. </param>
    /// <param name="b"> Brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(float time, double h, double s, double b) => ColorHsb(OsbEasing.None, time, time, h, s, b, h, s, b);

    //==========P==========//
    /// <summary> Apply a parameter to an <see cref="OsbSprite"/> for a given duration. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="param"> The <see cref="CommandParameter"/> type to be applied. </param>
    public void Parameter(float startTime, float endTime, CommandParameter param)
        => addCommand(new ParameterCommand(startTime, endTime, param));

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
    public void Additive(float startTime, float endTime) => Parameter(startTime, endTime, CommandParameter.AdditiveBlending);

    /// <summary> Applies additive blending to an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    public void Additive(float time) => Additive(time, time);

    /// <summary> Repeat commands <paramref name="loopCount"/> times until <see cref="EndGroup"/> is called. </summary>
    /// <remarks> Command times inside the loop are relative to the <paramref name="startTime"/> of the loop. </remarks>
    /// <param name="startTime"> Start time of the loop. </param>
    /// <param name="loopCount"> How many times the loop should repeat. </param>
    public LoopCommand StartLoopGroup(float startTime, int loopCount)
    {
        LoopCommand loopCommand = new(startTime, loopCount);
        addCommand(loopCommand);
        startDisplayLoop(loopCommand);
        return loopCommand;
    }

    /// <summary>
    ///     Commands on the <see cref="OsbSprite"/> until <see cref="EndGroup"/> is called will be active when the
    ///     <paramref name="triggerName"/> event happens until <paramref name="endTime"/>.
    /// </summary>
    /// <remarks> Command times inside the loop are relative to the <paramref name="startTime"/> of the trigger loop. </remarks>
    /// <param name="triggerName"> Trigger type of the loop </param>
    /// <param name="startTime"> Start time of the loop. </param>
    /// <param name="endTime"> End time of the loop. </param>
    /// <param name="group"> Group number of the loop. </param>
    public TriggerCommand StartTriggerGroup(string triggerName, float startTime, float endTime, int group = 0)
    {
        TriggerCommand triggerCommand = new(triggerName, startTime, endTime, group);
        addCommand(triggerCommand);
        startDisplayTrigger(triggerCommand);
        HasTrigger = true;
        return triggerCommand;
    }

    ///<summary> Calls the end of a loop. </summary>
    public void EndGroup()
    {
        currentCommandGroup.EndGroup();
        currentCommandGroup = null;

        endDisplayComposites();
    }

    void addCommand(ICommand command)
    {
        if (command is CommandGroup commandGroup)
        {
            currentCommandGroup = commandGroup;
            commandGroups.Add(commandGroup);
        }
        else
        {
            currentCommandGroup?.Add(command);
            addDisplayCommand(command);
        }

        clearStartEndTimes();

        HasOverlappedCommands = MoveTimeline.HasOverlap ||
            MoveXTimeline.HasOverlap ||
            MoveYTimeline.HasOverlap ||
            ScaleTimeline.HasOverlap ||
            ScaleVecTimeline.HasOverlap ||
            RotateTimeline.HasOverlap ||
            FadeTimeline.HasOverlap ||
            ColorTimeline.HasOverlap ||
            AdditiveTimeline.HasOverlap ||
            FlipHTimeline.HasOverlap ||
            FlipVTimeline.HasOverlap;

        HasIncompatibleCommands = MoveTimeline.HasCommands && (MoveXTimeline.HasCommands || MoveYTimeline.HasCommands) ||
            ScaleTimeline.HasCommands && ScaleVecTimeline.HasCommands;

        HasMoveCommands = MoveXTimeline.HasCommands || MoveYTimeline.HasCommands || MoveTimeline.HasCommands;
        HasScalingCommands = ScaleTimeline.HasCommands || ScaleVecTimeline.HasCommands;
    }

    /// <summary> Adds a command to be run on the sprite. </summary>
    /// <param name="command"> The command type to be run. </param>
    /// <param name="offset"></param>
    public void AddCommand(ICommand command, float offset = 0)
    {
        switch (command)
        {
            case ColorCommand color:
                Color(color.Easing, color.StartTime, color.EndTime + offset, color.StartValue, color.EndValue); break;

            case FadeCommand fade:
                Fade(fade.Easing, fade.StartTime + offset, fade.EndTime + offset, fade.StartValue, fade.EndValue); break;

            case ScaleCommand scale:
                Scale(scale.Easing,
                    scale.StartTime + offset,
                    scale.EndTime + offset,
                    scale.StartValue,
                    scale.EndValue); break;

            case VScaleCommand vScale:
                ScaleVec(vScale.Easing,
                    vScale.StartTime + offset,
                    vScale.EndTime + offset,
                    vScale.StartValue,
                    vScale.EndValue); break;

            case ParameterCommand param:
                Parameter(param.StartTime + offset, param.EndTime + offset, param.StartValue); break;

            case MoveCommand move:
                Move(move.Easing, move.StartTime + offset, move.EndTime + offset, move.StartValue, move.EndValue); break;

            case MoveXCommand moveX:
                MoveX(moveX.Easing,
                    moveX.StartTime + offset,
                    moveX.EndTime + offset,
                    moveX.StartValue,
                    moveX.EndValue); break;

            case MoveYCommand moveY:
                MoveY(moveY.Easing,
                    moveY.StartTime + offset,
                    moveY.EndTime + offset,
                    moveY.StartValue,
                    moveY.EndValue); break;

            case RotateCommand rotate:
                Rotate(rotate.Easing,
                    rotate.StartTime + offset,
                    rotate.EndTime + offset,
                    rotate.StartValue,
                    rotate.EndValue); break;

            case LoopCommand loop:
            {
                StartLoopGroup(loop.StartTime + offset, loop.LoopCount);
                foreach (var cmd in loop.Commands) addCommand(cmd);
                EndGroup();
                break;
            }

            case TriggerCommand trigger:
            {
                StartTriggerGroup(trigger.TriggerName, trigger.StartTime + offset, trigger.EndTime + offset, trigger.Group);
                foreach (var cmd in trigger.Commands) addCommand(cmd);
                EndGroup();
                break;
            }

            default:
                throw new NotSupportedException($"Failed to add command: No support for adding command of type {
                    command.GetType().FullName}");
        }
    }

    /// <returns> True if the sprite is active at <paramref name="time"/>, else returns false. </returns>
    public bool IsActive(float time) => StartTime <= time && time <= EndTime;

    public bool ShouldBeActive(float time) => DisplayStartTime <= time && time <= DisplayEndTime;

    ///<summary> Writes this sprite's data to a stream. </summary>
    public override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        StoryboardTransform transform)
    {
        if (CommandCost == 0) return;

        WriteHeader(writer, exportSettings, layer, transform);
        foreach (var command in commandGroups.AsValueEnumerable()
            .Concat(displayValueBuilders.AsValueEnumerable().SelectMany(c => c.Item2.Commands.AsValueEnumerable())))
            command.WriteOsb(writer, exportSettings, transform, 1);
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
                (CommandPosition)transform.ApplyToPositionXY(InitialPosition) : transform.ApplyToPosition(InitialPosition);

        using var builder = TempList.Create<char>();
        builder.AppendEnum(layer);
        builder.Add(',');

        builder.AppendEnum(Origin);
        builder.Add(',');

        builder.Add('"');
        builder.AddRange(texturePath.AsSpan().Trim());
        builder.Add('"');

        builder.Add(',');

        if (!MoveTimeline.HasCommands && !MoveXTimeline.HasCommands)
        {
            using var str = transformedInitialPosition.X.ToOsbString(exportSettings);
            builder.AddRange(str.AsReadOnlySpan());
        }
        else builder.Add('0');

        builder.Add(',');
        if (!MoveTimeline.HasCommands && !MoveYTimeline.HasCommands)
        {
            using var str = transformedInitialPosition.Y.ToOsbString(exportSettings);
            builder.AddRange(str.AsReadOnlySpan());
        }
        else builder.Add('0');

        writer.Write(builder.AsReadOnlySpan());
    }

    /// <summary> Returns whether the sprite is within widescreen storyboard bounds. </summary>
    /// <param name="position"> The storyboard position, in osu!pixels, of the sprite. </param>
    /// <param name="size"> The image dimensions of the sprite texture. </param>
    /// <param name="rotation"> The rotation, in radians, of the sprite. </param>
    /// <param name="origin"> The <see cref="OsbOrigin"/> of the sprite. </param>
    public static bool InScreenBounds(CommandPosition position, CommandScale size, CommandDecimal rotation, OsbOrigin origin)
        => new OrientedBoundingBox(position, GetOriginVector(origin, size), size.X, size.Y, rotation).Intersects(
            in OsuHitObject.WidescreenStoryboardBounds);

    /// <summary> Gets the origin of a sprite based on its <see cref="OsbOrigin"/> </summary>
    /// <param name="origin"> The <see cref="OsbOrigin"/> to be taken into account. </param>
    /// <param name="size"> The size of the sprite. </param>
    public static Vector2 GetOriginVector(OsbOrigin origin, Vector2 size) => origin switch
    {
        OsbOrigin.TopLeft => Vector2.Zero,
        OsbOrigin.TopCentre => new(size.X * .5f, 0),
        OsbOrigin.TopRight => size with { Y = 0 },
        OsbOrigin.CentreLeft => new(0, size.Y * .5f),
        OsbOrigin.Centre => new(size.X * .5f, size.Y * .5f),
        OsbOrigin.CentreRight => size with { Y = size.Y * .5f },
        OsbOrigin.BottomLeft => size with { X = 0 },
        OsbOrigin.BottomCentre => size with { X = size.X * .5f },
        OsbOrigin.BottomRight => size,
        _ => throw new NotSupportedException(origin.ToString())
    };

    #region Display

    internal readonly (Func<ICommand, bool>, CommandTimeline)[] displayValueBuilders;
    public readonly CommandTimeline<CommandPosition> MoveTimeline = new();

    public readonly CommandTimeline<CommandDecimal> MoveXTimeline = new(), MoveYTimeline = new(), ScaleTimeline = new(1),
        RotateTimeline = new(), FadeTimeline = new(1);

    public readonly CommandTimeline<CommandScale> ScaleVecTimeline = new(Vector2.One);
    public readonly CommandTimeline<CommandColor> ColorTimeline = new(CommandColor.White);

    public readonly CommandTimeline<CommandParameter> AdditiveTimeline = new(CommandParameter.None),
        FlipHTimeline = new(CommandParameter.None), FlipVTimeline = new(CommandParameter.None);

    /// <summary> Retrieves the <see cref="CommandPosition"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandPosition PositionAt(float time) => MoveTimeline.HasCommands ?
        MoveTimeline.ValueAtTime(time) :
        new(MoveXTimeline.ValueAtTime(time), MoveYTimeline.ValueAtTime(time));

    /// <summary> Retrieves the <see cref="CommandScale"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandScale ScaleAt(float time) => ScaleVecTimeline.HasCommands ?
        ScaleVecTimeline.ValueAtTime(time) :
        new(ScaleTimeline.ValueAtTime(time));

    /// <summary> Retrieves the rotation, in radians, of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandDecimal RotationAt(float time) => RotateTimeline.ValueAtTime(time);

    /// <summary> Retrieves the opacity level of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandDecimal OpacityAt(float time) => FadeTimeline.ValueAtTime(time);

    /// <summary> Retrieves the <see cref="CommandColor"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandColor ColorAt(float time) => ColorTimeline.ValueAtTime(time);

    /// <summary> Retrieves the additive value of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandParameter AdditiveAt(float time) => AdditiveTimeline.ValueAtTime(time);

    /// <summary> Retrieves the horizontal flip of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandParameter FlipHAt(float time) => FlipHTimeline.ValueAtTime(time);

    /// <summary> Retrieves the vertical flip of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandParameter FlipVAt(float time) => FlipVTimeline.ValueAtTime(time);

    void addDisplayCommand(ICommand command)
    {
        foreach (var (predicate, timeline) in displayValueBuilders)
            if (predicate(command))
            {
                var result = timeline.Add(command);
                if (result) ++CommandCost;

                return;
            }
    }

    void startDisplayLoop(LoopCommand loopCommand)
    {
        foreach (var builders in displayValueBuilders) builders.Item2.StartGroup(loopCommand);
    }

    void startDisplayTrigger(TriggerCommand triggerCommand)
    {
        foreach (var builders in displayValueBuilders) builders.Item2.StartGroup(triggerCommand);
    }

    void endDisplayComposites()
    {
        foreach (var builders in displayValueBuilders) builders.Item2.EndGroup();
    }

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