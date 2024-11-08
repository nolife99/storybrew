namespace StorybrewCommon.Storyboarding.Util;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Animations;
using Commands;
using CommandValues;
using Scripting;

/// <summary> Generates commands on an <see cref="OsbSprite" /> based on the states of that sprite. </summary>
public class CommandGenerator
{
    readonly KeyframedValue<CommandColor> colors = new(InterpolatingFunctions.CommandColor),
        finalColors = new(InterpolatingFunctions.CommandColor);

    readonly KeyframedValue<bool> flipH = new(InterpolatingFunctions.BoolFrom),
        flipV = new(InterpolatingFunctions.BoolFrom), additive = new(InterpolatingFunctions.BoolFrom);

    readonly KeyframedValue<CommandPosition> positions = new(InterpolatingFunctions.Position),
        finalPositions = new(InterpolatingFunctions.Position);

    readonly KeyframedValue<float> rotations = new(InterpolatingFunctions.FloatAngle),
        fades = new(InterpolatingFunctions.Float), finalRotations = new(InterpolatingFunctions.FloatAngle),
        finalFades = new(InterpolatingFunctions.Float);

    readonly KeyframedValue<CommandScale> scales = new(InterpolatingFunctions.Scale),
        finalScales = new(InterpolatingFunctions.Scale);

    readonly List<State> states = [];

    ///<summary> The tolerance threshold for coloring keyframe simplification. </summary>
    public float ColorTolerance = 1;

    ///<summary> The amount of decimal digits for opacity keyframes. </summary>
    public int OpacityDecimals = 1;

    ///<summary> The tolerance threshold for opacity keyframe simplification. </summary>
    public float OpacityTolerance = 1;

    ///<summary> The amount of decimal digits for position keyframes. </summary>
    public int PositionDecimals = 1;

    ///<summary> The tolerance threshold for position keyframe simplification. </summary>
    public float PositionTolerance = 1;

    ///<summary> The amount of decimal digits for rotation keyframes. </summary>
    public int RotationDecimals = 5;

    ///<summary> The tolerance threshold for rotation keyframe simplification. </summary>
    public float RotationTolerance = 1;

    ///<summary> The amount of decimal digits for scaling keyframes. </summary>
    public int ScaleDecimals = 3;

    ///<summary> The tolerance threshold for scaling keyframe simplification. </summary>
    public float ScaleTolerance = 1;

    /// <summary> Gets the <see cref="CommandGenerator" />'s start state. </summary>
    public State StartState => states.Count == 0 ? null : states[0];

    /// <summary> Gets the <see cref="CommandGenerator" />'s end state. </summary>
    public State EndState => states.Count == 0 ? null : states[^1];

    /// <summary> Adds a <see cref="State" /> to this instance that will be automatically sorted. </summary>
    public void Add(State state)
    {
        var count = states.Count;
        if (count == 0 || states[count - 1].Time <= state.Time)
        {
            states.Add(state);
            return;
        }

        var i = states.BinarySearch(state, state);
        if (i >= 0)
            while (i < count - 1 && states[i + 1].Time <= state.Time)
                ++i;
        else
            i = ~i;

        states.Insert(i, state);
    }

    /// <summary> Generates commands on a sprite based on this generator's states. </summary>
    /// <param name="sprite"> The <see cref="OsbSprite" /> to have commands generated on. </param>
    /// <param name="action"> Encapsulates a group of commands to be generated on <paramref name="sprite" />. </param>
    /// <param name="startTime">
    ///     The explicit start time of the command generation. Can be left <see langword="null" /> if
    ///     <see cref="State.Time" /> is used.
    /// </param>
    /// <param name="endTime">
    ///     The explicit end time of the command generation. Can be left <see langword="null" /> if
    ///     <see cref="State.Time" /> is used.
    /// </param>
    /// <param name="timeOffset"> The time offset of the command times. </param>
    /// <param name="loopable"> Whether the commands to be generated are contained within a <see cref="LoopCommand" />. </param>
    /// <returns> <see langword="true" /> if any commands were generated, else returns <see langword="false" />. </returns>
    public bool GenerateCommands(OsbSprite sprite, Action<Action, OsbSprite> action = null, float? startTime = null,
        float? endTime = null, float timeOffset = 0, bool loopable = false)
    {
        if (states.Count == 0) return false;

        State previousState = null;
        bool wasVisible = false, everVisible = false, stateAdded = false;
        var imageSize = BitmapDimensions(sprite.TexturePath);

        foreach (var state in states)
        {
            var time = state.Time + timeOffset;
            if (sprite is OsbAnimation) imageSize = BitmapDimensions(sprite.GetTexturePathAt(time));
            var isVisible = state.IsVisible(imageSize, sprite.Origin, this);

            if (isVisible && !everVisible) everVisible = true;
            switch (wasVisible)
            {
                case false when isVisible:
                    if (!stateAdded && previousState is not null)
                        addKeyframes(previousState, loopable ? time : previousState.Time + timeOffset);
                    addKeyframes(state, time);
                    if (!stateAdded) stateAdded = true;
                    break;
                case true when !isVisible:
                    addKeyframes(state, time);
                    commitKeyframes(imageSize);
                    break;
                default:
                {
                    if (isVisible)
                        addKeyframes(state, time);
                    else
                        stateAdded = false;
                    break;
                }
            }

            previousState = state;
            wasVisible = isVisible;
        }

        if (wasVisible) commitKeyframes(imageSize);
        if (everVisible)
        {
            if (action is null)
                convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable);
            else
                action(() => convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable), sprite);
        }

        clearKeyframes();
        return everVisible;
    }

    void commitKeyframes(SizeF imageSize)
    {
        fades.Simplify1dKeyframes(OpacityTolerance, f => Math.Clamp(f * 100, 0, 100));
        if (Math.Round(fades.StartValue, OpacityDecimals) > 0) fades.Add(fades.StartTime, 0, true);
        if (Math.Round(fades.EndValue, OpacityDecimals) > 0) fades.Add(fades.EndTime, 0);
        fades.TransferKeyframes(finalFades);

        positions.Simplify2dKeyframes(PositionTolerance, s => s);
        finalPositions.Until(positions.StartTime);
        positions.TransferKeyframes(finalPositions);

        scales.Simplify2dKeyframes(ScaleTolerance, v => new(v.X * imageSize.Width, v.Y * imageSize.Height));
        finalScales.Until(scales.StartTime);
        scales.TransferKeyframes(finalScales);

        rotations.Simplify1dKeyframes(RotationTolerance, r => 180 / MathF.PI * r);
        finalRotations.Until(rotations.StartTime);
        rotations.TransferKeyframes(finalRotations);

        colors.Simplify3dKeyframes(ColorTolerance, c => new(c.R, c.G, c.B));
        finalColors.Until(colors.StartTime);
        colors.TransferKeyframes(finalColors);
    }

    void convertToCommands(OsbSprite sprite, float? startTime, float? endTime, float timeOffset, SizeF imageSize,
        bool loopable)
    {
        float? startState = loopable ? (startTime ?? StartState.Time) + timeOffset : null,
            endState = loopable ? (endTime ?? EndState.Time) + timeOffset : null;

        bool moveX = finalPositions.All(k => checkPos(k.Value.Y) == checkPos(finalPositions.StartValue.Y)),
            moveY = finalPositions.All(k => checkPos(k.Value.X) == checkPos(finalPositions.StartValue.X));

        finalPositions.ForEachPair((s, e) =>
            {
                if (moveX && !moveY)
                {
                    sprite.MoveX(s.Time, e.Time, s.Value.X, e.Value.X);
                    sprite.InitialPosition = new(0, s.Value.Y);
                }
                else if (moveY && !moveX)
                {
                    sprite.MoveY(s.Time, e.Time, s.Value.Y, e.Value.Y);
                    sprite.InitialPosition = new(s.Value.X, 0);
                }
                else
                    sprite.Move(s.Time, e.Time, s.Value, e.Value);
            }, new(320, 240), p => new(MathF.Round(p.X, PositionDecimals), MathF.Round(p.Y, PositionDecimals)),
            startState,
            endState, loopable);

        var vec = finalScales.Any(k => Math.Abs(checkScale(k.Value.X) - checkScale(k.Value.Y)) > 1);
        finalScales.ForEachPair((s, e) =>
            {
                if (vec)
                    sprite.ScaleVec(s.Time, e.Time, s.Value, e.Value);
                else
                    sprite.Scale(s.Time, e.Time, s.Value.X, e.Value.X);
            }, Vector2.One, s => new(MathF.Round(s.X, ScaleDecimals), MathF.Round(s.Y, ScaleDecimals)), startState,
            endState, loopable);

        finalRotations.ForEachPair((s, e) => sprite.Rotate(s.Time, e.Time, s.Value, e.Value), 0,
            r => MathF.Round(r, RotationDecimals), startState, endState, loopable);
        finalColors.ForEachPair((s, e) => sprite.Color(s.Time, e.Time, s.Value, e.Value), CommandColor.White, null,
            startState, endState, loopable);
        finalFades.ForEachPair((s, e) =>
        {
            // what the hell???
            if (!(s.Time == sprite.StartTime && s.Time == e.Time && e.Value >= 1 || s.Time == sprite.EndTime ||
                s.Time == EndState.Time && s.Time == e.Time && e.Value <= 0))
                sprite.Fade(s.Time, e.Time, s.Value, e.Value);
        }, -1, o => MathF.Round(o, OpacityDecimals), startState, endState, loopable);

        flipH.ForEachFlag(sprite.FlipH);
        flipV.ForEachFlag(sprite.FlipV);
        additive.ForEachFlag(sprite.Additive);
        return;

        float checkScale(float value) => value * Math.Max(imageSize.Width, imageSize.Height);
        float checkPos(float value) => MathF.Round(value, PositionDecimals);
    }

    void addKeyframes(State state, float time)
    {
        positions.Add(time, state.Position);
        scales.Add(time, state.Scale);
        rotations.Add(time, state.Rotation);
        colors.Add(time, state.Color);
        fades.Add(time, state.Opacity);
        flipH.Add(time, state.FlipH);
        flipV.Add(time, state.FlipV);
        additive.Add(time, state.Additive);
    }

    void clearKeyframes()
    {
        states.Clear();
        states.Capacity = 0;

        positions.Clear(true);
        scales.Clear(true);
        rotations.Clear(true);
        colors.Clear(true);
        fades.Clear(true);
        finalPositions.Clear(true);
        finalScales.Clear(true);
        finalRotations.Clear(true);
        finalColors.Clear(true);
        finalFades.Clear(true);
        flipH.Clear(true);
        flipV.Clear(true);
        additive.Clear(true);
    }

    internal static SizeF BitmapDimensions(string path)
        => StoryboardObjectGenerator.Current.GetMapsetBitmap(path, StoryboardObjectGenerator.Current.fonts.Count == 0)
            .PhysicalDimension;
}

/// <summary> Defines all of an <see cref="OsbSprite" />'s states as a class. </summary>
public class State : IComparer<State>
{
    ///<summary> Represents the additive toggle condition of this state. </summary>
    public bool Additive;

    ///<summary> Represents the color, in RGB values, of this state. </summary>
    public CommandColor Color = CommandColor.White;

    ///<summary> Represents the horizontal flip condition of this state. </summary>
    public bool FlipH;

    ///<summary> Represents the vertical flip condition of this state. </summary>
    public bool FlipV;

    ///<summary> Represents the opacity, from 0 to 1, of this state. </summary>
    public float Opacity;

    ///<summary> Represents the position, in osu!pixels, of this state. </summary>
    public CommandPosition Position = new(320, 240);

    ///<summary> Represents the rotation, in radians, of this state. </summary>
    public float Rotation;

    ///<summary> Represents the scale, in osu!pixels, of this state. </summary>
    public CommandScale Scale = CommandScale.One;

    ///<summary> Represents the base time, in milliseconds, of this state. </summary>
    public float Time;

    int IComparer<State>.Compare(State x, State y) => Math.Sign(x.Time - y.Time);

    /// <summary>
    ///     Determines the visibility of the sprite in the current <see cref="State" /> based on its image dimensions and
    ///     <see cref="OsbOrigin" />.
    /// </summary>
    /// <returns>
    ///     <see langword="true" /> if the sprite is visible within widescreen boundaries, else returns
    ///     <see langword="false" />.
    /// </returns>
    public bool IsVisible(SizeF imageSize, OsbOrigin origin, CommandGenerator generator = null)
    {
        var noGen = generator is null;
        CommandScale scale = new(noGen ? (float)Scale.X : MathF.Round(Scale.X, generator.ScaleDecimals),
            noGen ? (float)Scale.Y : MathF.Round(Scale.Y, generator.ScaleDecimals));

        if (Additive && Color == CommandColor.Black ||
            (noGen ? Opacity : Math.Round(Opacity, generator.OpacityDecimals)) <= 0 || scale.X <= 0 || scale.Y <= 0)
            return false;

        return OsbSprite.InScreenBounds(
            new(noGen ? (float)Position.X : MathF.Round(Position.X, generator.PositionDecimals),
                noGen ? (float)Position.Y : MathF.Round(Position.Y, generator.PositionDecimals)), imageSize * scale,
            noGen ? Rotation : Math.Round(Rotation, generator.RotationDecimals), origin);
    }
}