﻿namespace StorybrewCommon.Storyboarding;

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

///<summary> Base sprite in storyboards. </summary>
public class OsbSprite : StoryboardObject
{
    ///<summary> Default position of sprites, unless modified elsewhere. </summary>
    public static readonly CommandPosition DefaultPosition = new(320, 240);

    readonly HashSet<ICommand> commands = [];

    float commandsStartTime = float.MaxValue, commandsEndTime = float.MinValue;
    CommandGroup currentCommandGroup;

    CommandPosition initialPosition;

    ///<summary> Origin of this sprite. </summary>
    public OsbOrigin Origin = OsbOrigin.Centre;

    string texturePath = "";

    ///<summary> Constructs a new abstract sprite. </summary>
    public OsbSprite()
    {
        initializeDisplayValueBuilders();
        InitialPosition = DefaultPosition;
    }

    /// <summary> If the sprite has more commands than this amount, they will be split between multiple sprites. </summary>
    /// <remarks> Does not apply when the sprite has triggers. </remarks>
    public int CommandSplitThreshold { get; set; }

    ///<returns> True if the sprite is in a command group, else returns false. </returns>
    public bool InGroup => currentCommandGroup is not null;

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
            moveTimeline.DefaultValue = initialPosition;
            moveXTimeline.DefaultValue = initialPosition.X;
            moveYTimeline.DefaultValue = initialPosition.Y;
        }
    }

    ///<summary> Gets a list of commands on this sprite. </summary>
    public IEnumerable<ICommand> Commands => commands;

    /// <returns> The total amount of commands being run on this instance of the <see cref="OsbSprite"/>. </returns>
    public int CommandCount => commands.Count;

    /// <returns> The total amount of commands, including loops, being run on this instance of the <see cref="OsbSprite"/>. </returns>
    public int CommandCost
    {
        get
        {
            var total = 0;
            foreach (var c in commands) total += c.Cost;
            return total;
        }
    }

    /// <returns> True if the <see cref="OsbSprite"/> has incompatible commands, else returns false. </returns>
    public bool HasIncompatibleCommands
        => moveTimeline.HasCommands && (moveXTimeline.HasCommands || moveYTimeline.HasCommands) ||
            scaleTimeline.HasCommands && scaleVecTimeline.HasCommands;

    /// <returns> True if the <see cref="OsbSprite"/> has overlapping commands, else returns false. </returns>
    public bool HasOverlappedCommands => moveTimeline.HasOverlap ||
        moveXTimeline.HasOverlap ||
        moveYTimeline.HasOverlap ||
        scaleTimeline.HasOverlap ||
        scaleVecTimeline.HasOverlap ||
        rotateTimeline.HasOverlap ||
        fadeTimeline.HasOverlap ||
        colorTimeline.HasOverlap ||
        additiveTimeline.HasOverlap ||
        flipHTimeline.HasOverlap ||
        flipVTimeline.HasOverlap;

    public bool HasRotateCommands => rotateTimeline.HasCommands;
    public bool HasScalingCommands => scaleTimeline.HasCommands || scaleVecTimeline.HasCommands;
    public bool HasMoveXYCommands => moveXTimeline.HasCommands || moveYTimeline.HasCommands;

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
        foreach (var command in commands)
            if (command.Active)
            {
                commandsStartTime = Math.Min(commandsStartTime, command.StartTime);
                commandsEndTime = Math.Max(commandsEndTime, command.EndTime);
            }
    }

    void clearStartEndTimes()
    {
        commandsStartTime = float.MaxValue;
        commandsEndTime = float.MinValue;
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
        => Move(default, startTime, endTime, startPosition, endPosition);

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startPosition"> Start <see cref="CommandPosition"/> value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(float startTime, float endTime, CommandPosition startPosition, double endX, double endY)
        => Move(default, startTime, endTime, startPosition, endX, endY);

    /// <summary>Change the position of an <see cref="OsbSprite"/> over time. Commands similar to MoveX are available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start-X value of the command. </param>
    /// <param name="startY"> Start-Y value of the command. </param>
    /// <param name="endX"> End-X value of the command. </param>
    /// <param name="endY"> End-Y value of the command. </param>
    public void Move(float startTime, float endTime, double startX, double startY, double endX, double endY)
        => Move(default, startTime, endTime, startX, startY, endX, endY);

    /// <summary> Sets the position of an <see cref="OsbSprite"/>. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="position"> <see cref="CommandPosition"/> value of the command. </param>
    public void Move(float time, CommandPosition position) => Move(default, time, time, position, position);

    /// <summary> Sets the position of an <see cref="OsbSprite"/>. Commands similar to MoveX are available for MoveY. </summary>
    /// <remarks> Cannot be used with <see cref="MoveXCommand"/> or <see cref="MoveYCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="x"> X value of the command. </param>
    /// <param name="y"> Y value of the command. </param>
    public void Move(float time, double x, double y) => Move(default, time, time, x, y, x, y);

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
        => MoveX(default, startTime, endTime, startX, endX);

    /// <summary> Sets the X-Position of an <see cref="OsbSprite"/>. Commands are also available for MoveY.</summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="x"> X value of the command. </param>
    public void MoveX(float time, double x) => MoveX(default, time, time, x, x);

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
        => MoveY(default, startTime, endTime, startY, endY);

    /// <summary> Sets the Y-Position of an <see cref="OsbSprite"/>. Commands are also available for MoveX. </summary>
    /// <remarks> Cannot be used with <see cref="MoveCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="y"> Y value of the command. </param>
    public void MoveY(float time, float y) => MoveY(default, time, time, y, y);

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
        => Scale(default, startTime, endTime, startScale, endScale);

    /// <summary> Sets the size of a sprite. </summary>
    /// <remarks> Cannot be used with <see cref="VScaleCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="scale"> Scale of the command. </param>
    public void Scale(float time, double scale) => Scale(default, time, time, scale, scale);

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
        => ScaleVec(default, startTime, endTime, startScale, endScale);

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startScale"> Start <see cref="CommandScale"/> value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(float startTime, float endTime, CommandScale startScale, double endX, double endY)
        => ScaleVec(default, startTime, endTime, startScale, endX, endY);

    /// <summary> Change the vector scale of a sprite over time. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startX"> Start X-Scale value of the command. </param>
    /// <param name="startY"> Start Y-Scale value of the command. </param>
    /// <param name="endX"> End X-Scale value of the command. </param>
    /// <param name="endY"> End Y-Scale value of the command. </param>
    public void ScaleVec(float startTime, float endTime, double startX, double startY, double endX, double endY)
        => ScaleVec(default, startTime, endTime, startX, startY, endX, endY);

    /// <summary> Sets the vector scale of a sprite. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="scale"> <see cref="CommandScale"/> value of the command. </param>
    public void ScaleVec(float time, CommandScale scale) => ScaleVec(default, time, time, scale, scale);

    /// <summary> Sets the vector scale of a sprite. </summary>
    /// <remarks> Cannot be used with <see cref="ScaleCommand"/>. </remarks>
    /// <param name="time"> Time of the command. </param>
    /// <param name="x"> Scale-X value of the command. </param>
    /// <param name="y"> Scale-Y value of the command. </param>
    public void ScaleVec(float time, double x, double y) => ScaleVec(default, time, time, x, y, x, y);

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
        => Rotate(default, startTime, endTime, startRotation, endRotation);

    /// <summary> Sets the rotation of an <see cref="OsbSprite"/>. Angles are in radians. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="rotation"> Radians of the command. </param>
    public void Rotate(float time, double rotation) => Rotate(default, time, time, rotation, rotation);

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
        => Fade(default, startTime, endTime, startFade, endFade);

    /// <summary> Sets the opacity of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="fade"> Fade value of the command. </param>
    public void Fade(float time, double fade) => Fade(default, time, time, fade, fade);

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
        => Color(default, startTime, endTime, startColor, endColor);

    /// <summary> Change the RGB color of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endR"> End red value of the command. </param>
    /// <param name="endG"> End green value of the command. </param>
    /// <param name="endB"> End blue value of the command. </param>
    public void Color(float startTime, float endTime, CommandColor startColor, double endR, double endG, double endB)
        => Color(default, startTime, endTime, startColor, endR, endG, endB);

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
        double endB) => Color(default, startTime, endTime, startR, startG, startB, endR, endG, endB);

    /// <summary> Sets the RGB color of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="color"> The <see cref="CommandColor"/> value of the command. </param>
    public void Color(float time, CommandColor color) => Color(default, time, time, color, color);

    /// <summary> Sets the RGB color of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="r"> Red value of the command. </param>
    /// <param name="g"> Green value of the command. </param>
    /// <param name="b"> Blue value of the command. </param>
    public void Color(float time, double r, double g, double b) => Color(default, time, time, r, g, b, r, g, b);

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
        => ColorHsb(default, startTime, endTime, startH, startS, startB, endColor);

    /// <summary> Change the hue, saturation, and brightness of an <see cref="OsbSprite"/> over time. </summary>
    /// <param name="startTime"> Start time of the command. </param>
    /// <param name="endTime"> End time of the command. </param>
    /// <param name="startColor"> Start <see cref="CommandColor"/> value of the command. </param>
    /// <param name="endH"> End hue value (in degrees) of the command. </param>
    /// <param name="endS"> End saturation value (from 0 to 1) of the command. </param>
    /// <param name="endB"> End brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(float startTime, float endTime, CommandColor startColor, double endH, double endS, double endB)
        => ColorHsb(default, startTime, endTime, startColor, endH, endS, endB);

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
        double endB) => ColorHsb(default, startTime, endTime, startH, startS, startB, endH, endS, endB);

    /// <summary> Sets the hue, saturation, and brightness of an <see cref="OsbSprite"/>. </summary>
    /// <param name="time"> Time of the command. </param>
    /// <param name="h"> Hue value (in degrees) of the command. </param>
    /// <param name="s"> Saturation value (from 0 to 1) of the command. </param>
    /// <param name="b"> Brightness level (from 0 to 1) of the command. </param>
    public void ColorHsb(float time, double h, double s, double b) => ColorHsb(default, time, time, h, s, b, h, s, b);

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
            commands.Add(commandGroup);
        }
        else if (currentCommandGroup?.Add(command) ?? commands.Add(command)) addDisplayCommand(command);

        clearStartEndTimes();
    }

    /// <summary> Adds a command to be run on the sprite. </summary>
    /// <param name="command"> The command type to be run. </param>
    public void AddCommand(ICommand command)
    {
        switch (command)
        {
            case ColorCommand color:
                Color(color.Easing, color.StartTime, color.EndTime, color.StartValue, color.EndValue); break;

            case FadeCommand fade: Fade(fade.Easing, fade.StartTime, fade.EndTime, fade.StartValue, fade.EndValue); break;

            case ScaleCommand scale:
                Scale(scale.Easing, scale.StartTime, scale.EndTime, scale.StartValue, scale.EndValue); break;

            case VScaleCommand vScale:
                ScaleVec(vScale.Easing, vScale.StartTime, vScale.EndTime, vScale.StartValue, vScale.EndValue); break;

            case ParameterCommand param: Parameter(param.StartTime, param.EndTime, param.StartValue); break;
            case MoveCommand move: Move(move.Easing, move.StartTime, move.EndTime, move.StartValue, move.EndValue); break;

            case MoveXCommand moveX:
                MoveX(moveX.Easing, moveX.StartTime, moveX.EndTime, moveX.StartValue, moveX.EndValue); break;

            case MoveYCommand moveY:
                MoveY(moveY.Easing, moveY.StartTime, moveY.EndTime, moveY.StartValue, moveY.EndValue); break;

            case RotateCommand rotate:
                Rotate(rotate.Easing, rotate.StartTime, rotate.EndTime, rotate.StartValue, rotate.EndValue); break;

            case LoopCommand loop:
            {
                StartLoopGroup(loop.StartTime, loop.LoopCount);
                foreach (var cmd in loop.Commands) AddCommand(cmd);
                EndGroup();
                break;
            }

            case TriggerCommand trigger:
            {
                StartTriggerGroup(trigger.TriggerName, trigger.StartTime, trigger.EndTime, trigger.Group);
                foreach (var cmd in trigger.Commands) AddCommand(cmd);
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

    ///<summary> Writes this sprite's data to a stream. </summary>
    public override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        StoryboardTransform transform)
    {
        if (commands.Count != 0)
            OsbWriterFactory.CreateWriter(this,
                    moveTimeline,
                    moveXTimeline,
                    moveYTimeline,
                    scaleTimeline,
                    scaleVecTimeline,
                    rotateTimeline,
                    fadeTimeline,
                    colorTimeline,
                    writer,
                    exportSettings,
                    layer)
                .WriteOsb(ref transform);
    }

    /// <summary> Returns whether the sprite is within widescreen storyboard bounds. </summary>
    /// <param name="position"> The storyboard position, in osu!pixels, of the sprite. </param>
    /// <param name="size"> The image dimensions of the sprite texture. </param>
    /// <param name="rotation"> The rotation, in radians, of the sprite. </param>
    /// <param name="origin"> The <see cref="OsbOrigin"/> of the sprite. </param>
    public static bool InScreenBounds(CommandPosition position, CommandScale size, CommandDecimal rotation, OsbOrigin origin)
    {
        using OrientedBoundingBox box = new(position, GetOriginVector(origin, size), size.X, size.Y, rotation);
        return box.Intersects(in OsuHitObject.WidescreenStoryboardBounds);
    }

    /// <summary> Gets the origin of a sprite based on its <see cref="OsbOrigin"/> </summary>
    /// <param name="origin"> The <see cref="OsbOrigin"/> to be taken into account. </param>
    /// <param name="size"> The size of the sprite. </param>
    public static Vector2 GetOriginVector(OsbOrigin origin, Vector2 size) => origin switch
    {
        OsbOrigin.TopLeft => default,
        OsbOrigin.TopCentre => new(size.X * .5f, 0),
        OsbOrigin.TopRight => size with { Y = 0 },
        OsbOrigin.CentreLeft => new(0, size.Y * .5f),
        OsbOrigin.Centre => new(size.X * .5f, size.Y * .5f),
        OsbOrigin.CentreRight => size with { Y = size.Y * .5f },
        OsbOrigin.BottomLeft => size with { X = 0 },
        OsbOrigin.BottomCentre => size with { X = size.X * .5f },
        OsbOrigin.BottomRight => new(size.X, size.Y),
        _ => throw new NotSupportedException(origin.ToString())
    };

    #region Display

    (Func<ICommand, bool>, IAnimatedValueBuilder)[] displayValueBuilders;
    readonly AnimatedValue<CommandPosition> moveTimeline = new();

    readonly AnimatedValue<CommandDecimal> moveXTimeline = new(), moveYTimeline = new(), scaleTimeline = new(1),
        rotateTimeline = new(), fadeTimeline = new(1);

    readonly AnimatedValue<CommandScale> scaleVecTimeline = new(Vector2.One);
    readonly AnimatedValue<CommandColor> colorTimeline = new(CommandColor.White);

    readonly AnimatedValue<CommandParameter> additiveTimeline = new(CommandParameter.None),
        flipHTimeline = new(CommandParameter.None), flipVTimeline = new(CommandParameter.None);

    /// <summary> Retrieves the <see cref="CommandPosition"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandPosition PositionAt(float time) => moveTimeline.HasCommands ?
        moveTimeline.ValueAtTime(time) :
        new(moveXTimeline.ValueAtTime(time), moveYTimeline.ValueAtTime(time));

    /// <summary> Retrieves the <see cref="CommandScale"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandScale ScaleAt(float time) => scaleVecTimeline.HasCommands ?
        scaleVecTimeline.ValueAtTime(time) :
        new(scaleTimeline.ValueAtTime(time));

    /// <summary> Retrieves the rotation, in radians, of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandDecimal RotationAt(float time) => rotateTimeline.ValueAtTime(time);

    /// <summary> Retrieves the opacity level of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandDecimal OpacityAt(float time) => fadeTimeline.ValueAtTime(time);

    /// <summary> Retrieves the <see cref="CommandColor"/> of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandColor ColorAt(float time) => colorTimeline.ValueAtTime(time);

    /// <summary> Retrieves the additive value of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandParameter AdditiveAt(float time) => additiveTimeline.ValueAtTime(time);

    /// <summary> Retrieves the horizontal flip of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandParameter FlipHAt(float time) => flipHTimeline.ValueAtTime(time);

    /// <summary> Retrieves the vertical flip of a sprite at a given time. </summary>
    /// <param name="time"> Time to retrieve the information at. </param>
    public CommandParameter FlipVAt(float time) => flipVTimeline.ValueAtTime(time);

    void initializeDisplayValueBuilders() => displayValueBuilders =
    [
        (c => c is MoveCommand, new AnimatedValueBuilder<CommandPosition>(moveTimeline)),
        (c => c is MoveXCommand, new AnimatedValueBuilder<CommandDecimal>(moveXTimeline)),
        (c => c is MoveYCommand, new AnimatedValueBuilder<CommandDecimal>(moveYTimeline)),
        (c => c is ScaleCommand, new AnimatedValueBuilder<CommandDecimal>(scaleTimeline)),
        (c => c is VScaleCommand, new AnimatedValueBuilder<CommandScale>(scaleVecTimeline)),
        (c => c is RotateCommand, new AnimatedValueBuilder<CommandDecimal>(rotateTimeline)),
        (c => c is FadeCommand, new AnimatedValueBuilder<CommandDecimal>(fadeTimeline)),
        (c => c is ColorCommand, new AnimatedValueBuilder<CommandColor>(colorTimeline)),
        (c => c is ParameterCommand { StartValue.Type: ParameterType.AdditiveBlending },
            new AnimatedValueBuilder<CommandParameter>(additiveTimeline)),
        (c => c is ParameterCommand { StartValue.Type: ParameterType.FlipHorizontal },
            new AnimatedValueBuilder<CommandParameter>(flipHTimeline)),
        (c => c is ParameterCommand { StartValue.Type: ParameterType.FlipVertical },
            new AnimatedValueBuilder<CommandParameter>(flipVTimeline))
    ];

    void addDisplayCommand(ICommand command)
    {
        foreach (var builders in displayValueBuilders)
            if (builders.Item1(command))
                builders.Item2.Add(command);
    }

    void startDisplayLoop(LoopCommand loopCommand)
    {
        foreach (var builders in displayValueBuilders) builders.Item2.StartDisplayLoop(loopCommand);
    }

    void startDisplayTrigger(TriggerCommand triggerCommand)
    {
        foreach (var builders in displayValueBuilders) builders.Item2.StartDisplayTrigger(triggerCommand);
    }

    void endDisplayComposites()
    {
        foreach (var builders in displayValueBuilders) builders.Item2.EndDisplayComposite();
    }

    #endregion
}
#pragma warning disable CS1591
public enum OsbLayer
{
    Background, Fail, Pass, Foreground, Overlay
}

///<summary> Enumeration values determining the origin of a sprite/image. </summary>
public enum OsbOrigin
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
public enum OsbEasing
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
public enum OsbLoopType
{
    ///<summary> Loops the animation frames for the sprite's lifetime, repeating when the last frame is reached. </summary>
    LoopForever,

    ///<summary> Loops the animation frames for the sprite once, stopping at the last frame. </summary>
    LoopOnce
}

///<summary> Define the parameter type for a parameter command. </summary>
public enum ParameterType
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