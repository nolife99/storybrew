namespace StorybrewCommon.Storyboarding.Util;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Animations;
using BrewLib.Util;
using Commands;
using CommandValues;
using Scripting;

/// <summary> Generates commands on an <see cref="OsbSprite"/> based on the states of that sprite. </summary>
public class CommandGenerator
{
    readonly KeyframedValue<CommandColor> colors = new(InterpolatingFunctions.CommandColor),
        finalColors = new(InterpolatingFunctions.CommandColor);

    readonly KeyframedValue<bool> flipH = new(InterpolatingFunctions.BoolFrom), flipV = new(InterpolatingFunctions.BoolFrom),
        additive = new(InterpolatingFunctions.BoolFrom);

    readonly KeyframedValue<CommandPosition> positions = new(InterpolatingFunctions.Position),
        finalPositions = new(InterpolatingFunctions.Position);

    readonly KeyframedValue<float> rotations = new(InterpolatingFunctions.FloatAngle), fades = new(InterpolatingFunctions.Float),
        finalRotations = new(InterpolatingFunctions.FloatAngle), finalFades = new(InterpolatingFunctions.Float);

    readonly KeyframedValue<CommandScale> scales = new(InterpolatingFunctions.Scale),
        finalScales = new(InterpolatingFunctions.Scale);

    UnmanagedList<State> states = new();

    ///<summary> The tolerance threshold for coloring keyframe simplification. </summary>
    public float ColorTolerance { get; set; } = 1;

    ///<summary> The amount of decimal digits for opacity keyframes. </summary>
    public int OpacityDecimals { get; set; } = 1;

    ///<summary> The tolerance threshold for opacity keyframe simplification. </summary>
    public float OpacityTolerance { get; set; } = 1;

    ///<summary> The amount of decimal digits for position keyframes. </summary>
    public int PositionDecimals { get; set; } = 1;

    ///<summary> The tolerance threshold for position keyframe simplification. </summary>
    public float PositionTolerance { get; set; } = 1;

    ///<summary> The amount of decimal digits for rotation keyframes. </summary>
    public int RotationDecimals { get; set; } = 5;

    ///<summary> The tolerance threshold for rotation keyframe simplification. </summary>
    public float RotationTolerance { get; set; } = 1;

    ///<summary> The amount of decimal digits for scaling keyframes. </summary>
    public int ScaleDecimals { get; set; } = 3;

    ///<summary> The tolerance threshold for scaling keyframe simplification. </summary>
    public float ScaleTolerance { get; set; } = 1;

    /// <summary> Gets the <see cref="CommandGenerator"/>'s start state. </summary>
    /// <remarks> If there are no states, returns a null reference. It is up to the caller to check for this. </remarks>
    public ref State StartState => ref states.Count == 0 ? ref Unsafe.NullRef<State>() : ref states.GetReference(0);

    /// <summary> Gets the <see cref="CommandGenerator"/>'s end state. </summary>
    /// <remarks> If there are no states, returns a null reference. It is up to the caller to check for this. </remarks>
    public ref State EndState => ref states.Count == 0 ? ref Unsafe.NullRef<State>() : ref states.GetReference(states.Count - 1);

    /// <summary> Adds a <see cref="State"/> to this instance that will be automatically sorted. </summary>
    public void Add(State state)
    {
        var count = states.Count;

        if (count == 0 || states.GetReference(count - 1).Time <= state.Time)
        {
            states.Add(ref state);
            return;
        }

        var i = states.GetSpan().BinarySearch(state, state);
        if (i >= 0)
            while (i < count - 1 && states.GetReference(i + 1).Time <= state.Time)
                ++i;
        else i = ~i;

        states.Insert(i, ref state);
    }

    /// <summary> Generates commands on a sprite based on this generator's states. </summary>
    /// <param name="sprite"> The <see cref="OsbSprite"/> to have commands generated on. </param>
    /// <param name="action"> Encapsulates a group of commands to be generated on <paramref name="sprite"/>. </param>
    /// <param name="startTime">
    ///     The explicit start time of the command generation. Can be left <see langword="null"/> if
    ///     <see cref="State.Time"/> is used.
    /// </param>
    /// <param name="endTime">
    ///     The explicit end time of the command generation. Can be left <see langword="null"/> if
    ///     <see cref="State.Time"/> is used.
    /// </param>
    /// <param name="timeOffset"> The time offset of the command times. </param>
    /// <param name="loopable"> Whether the commands to be generated are contained within a <see cref="LoopCommand"/>. </param>
    /// <returns> <see langword="true"/> if any commands were generated, else returns <see langword="false"/>. </returns>
    public void GenerateCommands(OsbSprite sprite,
        Action<Action, OsbSprite> action = null,
        float? startTime = null,
        float? endTime = null,
        float timeOffset = 0,
        bool loopable = false)
    {
        if (states.Count == 0) return;

        ref var previousState = ref Unsafe.NullRef<State>();
        bool wasVisible = false, everVisible = false, stateAdded = false;
        var imageSize = BitmapDimensions(sprite.TexturePath);

        ensureCapacity();
        foreach (ref var state in states.GetSpan())
        {
            var time = state.Time + timeOffset;
            if (sprite is OsbAnimation) imageSize = BitmapDimensions(sprite.GetTexturePathAt(time));
            var isVisible = state.IsVisible(imageSize, sprite.Origin, this);

            if (isVisible && !everVisible) everVisible = true;
            switch (wasVisible)
            {
                case false when isVisible:
                    if (!stateAdded && !Unsafe.IsNullRef(ref previousState))
                        addKeyframes(ref previousState, loopable ? time : previousState.Time + timeOffset);

                    addKeyframes(ref state, time);
                    if (!stateAdded) stateAdded = true;
                    break;
                case true when !isVisible:
                    addKeyframes(ref state, time);
                    commitKeyframes(imageSize);
                    break;
                default:
                    if (isVisible) addKeyframes(ref state, time);
                    else stateAdded = false;

                    break;
            }

            previousState = ref state;
            wasVisible = isVisible;
        }

        if (wasVisible) commitKeyframes(imageSize);
        if (everVisible)
        {
            if (action is null) convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable);
            else action(() => convertToCommands(sprite, startTime, endTime, timeOffset, imageSize, loopable), sprite);
        }

        clearKeyframes();
    }

    void commitKeyframes(Vector2 imageSize)
    {
        fades.Simplify1dKeyframes(OpacityTolerance, f => f * 100);
        if (float.Round(fades.StartValue, OpacityDecimals) > 0) fades.Add(fades.StartTime, 0, true);
        if (float.Round(fades.EndValue, OpacityDecimals) > 0) fades.Add(fades.EndTime, 0);
        fades.TransferKeyframes(finalFades);

        positions.Simplify2dKeyframes(PositionTolerance, s => s);
        positions.TransferKeyframes(finalPositions.Until(positions.StartTime));

        scales.Simplify2dKeyframes(ScaleTolerance, v => (Vector2)v * imageSize);
        scales.TransferKeyframes(finalScales.Until(scales.StartTime));

        rotations.Simplify1dKeyframes(RotationTolerance, float.RadiansToDegrees);
        rotations.TransferKeyframes(finalRotations.Until(rotations.StartTime));

        colors.Simplify3dKeyframes(ColorTolerance, c => new(c.R, c.G, c.B));
        colors.TransferKeyframes(finalColors.Until(colors.StartTime));
    }

    void convertToCommands(OsbSprite sprite, float? startTime, float? endTime, float timeOffset, Vector2 imageSize, bool loopable)
    {
        float? startState = loopable ? (startTime ?? StartState.Time) + timeOffset : null,
            endState = loopable ? (endTime ?? EndState.Time) + timeOffset : null;

        bool moveX = finalPositions.keyframes.TrueForAll(k => checkPos(k.Value.Y) == checkPos(finalPositions.StartValue.Y)),
            moveY = finalPositions.keyframes.TrueForAll(k => checkPos(k.Value.X) == checkPos(finalPositions.StartValue.X));

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
                else sprite.Move(s.Time, e.Time, s.Value, e.Value);
            },
            new(320, 240),
            p => new(checkPos(p.X), checkPos(p.Y)),
            startState,
            endState,
            loopable);

        var scalar = finalScales.keyframes.TrueForAll(k => Math.Abs(checkScale(k.Value.X) - checkScale(k.Value.Y)) < 1);
        finalScales.ForEachPair((s, e) =>
            {
                if (scalar) sprite.Scale(s.Time, e.Time, s.Value.X, e.Value.X);
                else sprite.ScaleVec(s.Time, e.Time, s.Value, e.Value);
            },
            Vector2.One,
            s => new(float.Round(s.X, ScaleDecimals), float.Round(s.Y, ScaleDecimals)),
            startState,
            endState,
            loopable);

        finalRotations.ForEachPair((s, e) => sprite.Rotate(s.Time, e.Time, s.Value, e.Value),
            0,
            r => float.Round(r, RotationDecimals),
            startState,
            endState,
            loopable);

        finalColors.ForEachPair((s, e) => sprite.Color(s.Time, e.Time, s.Value, e.Value),
            CommandColor.White,
            null,
            startState,
            endState,
            loopable);

        finalFades.ForEachPair((s, e) =>
            {
                if (!(s.Time == sprite.StartTime && s.Time == e.Time && e.Value >= 1 ||
                    s.Time == sprite.EndTime ||
                    s.Time == EndState.Time && s.Time == e.Time && e.Value <= 0)) sprite.Fade(s.Time, e.Time, s.Value, e.Value);
            },
            -1,
            o => float.Round(o, OpacityDecimals),
            startState,
            endState,
            loopable);

        flipH.ForEachFlag(sprite.FlipH);
        flipV.ForEachFlag(sprite.FlipV);
        additive.ForEachFlag(sprite.Additive);
        return;

        float checkScale(float value) => value * Math.Max(imageSize.X, imageSize.Y);
        float checkPos(float value) => float.Round(value, PositionDecimals);
    }

    void ensureCapacity()
    {
        colors.keyframes.EnsureCapacity(states.Count);
        flipH.keyframes.EnsureCapacity(states.Count);
        flipV.keyframes.EnsureCapacity(states.Count);
        additive.keyframes.EnsureCapacity(states.Count);
        positions.keyframes.EnsureCapacity(states.Count);
        rotations.keyframes.EnsureCapacity(states.Count);
        fades.keyframes.EnsureCapacity(states.Count);
        scales.keyframes.EnsureCapacity(states.Count);
    }

    void addKeyframes(ref State state, float time)
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
        positions.Clear();
        scales.Clear();
        rotations.Clear();
        colors.Clear();
        fades.Clear();
        finalPositions.Clear();
        finalScales.Clear();
        finalRotations.Clear();
        finalColors.Clear();
        finalFades.Clear();
        flipH.Clear();
        flipV.Clear();
        additive.Clear();

        ((IDisposable)states).Dispose();
        states = new();
    }

    internal static Vector2 BitmapDimensions(string path)
    {
        var image = StoryboardObjectGenerator.Current.GetMapsetBitmap(path, StoryboardObjectGenerator.Current.fonts.Count == 0);

        return new(image.Width, image.Height);
    }
}

/// <summary> Defines all of an <see cref="OsbSprite"/>'s states as a class. </summary>
public record struct State : IComparer<State>
{
    ///<summary> Represents the additive toggle condition of this state. </summary>
    public bool Additive;

    ///<summary> Represents the color, in RGB values, of this state. </summary>
    public CommandColor Color;

    ///<summary> Represents the horizontal flip condition of this state. </summary>
    public bool FlipH;

    ///<summary> Represents the vertical flip condition of this state. </summary>
    public bool FlipV;

    ///<summary> Represents the opacity, from 0 to 1, of this state. </summary>
    public float Opacity;

    ///<summary> Represents the position, in osu!pixels, of this state. </summary>
    public CommandPosition Position;

    ///<summary> Represents the rotation, in radians, of this state. </summary>
    public float Rotation;

    ///<summary> Represents the scale, in osu!pixels, of this state. </summary>
    public CommandScale Scale;

    ///<summary> Represents the base time, in milliseconds, of this state. </summary>
    public float Time;

    /// <summary>
    ///     Creates a default state.
    /// </summary>
    public State()
    {
        Additive = FlipH = FlipV = false;
        Opacity = Rotation = Time = 0;
        Position = new(320, 240);
        Scale = CommandScale.One;
        Color = CommandColor.White;
    }

    int IComparer<State>.Compare(State x, State y) => Math.Sign(x.Time - y.Time);

    /// <summary>
    ///     Determines the visibility of the sprite in the current <see cref="State"/> based on its image dimensions
    ///     and <see cref="OsbOrigin"/>.
    /// </summary>
    /// <returns>
    ///     <see langword="true"/> if the sprite is visible within widescreen boundaries, else returns
    ///     <see langword="false"/>.
    /// </returns>
    public bool IsVisible(Vector2 imageSize, OsbOrigin origin, CommandGenerator generator = null)
    {
        var noGen = generator is null;
        Vector2 scale = new(noGen ? Scale.X : float.Round(Scale.X, generator.ScaleDecimals),
            noGen ? Scale.Y : float.Round(Scale.Y, generator.ScaleDecimals));

        if (Additive && Color == CommandColor.Black ||
            (noGen ? Opacity : float.Round(Opacity, generator.OpacityDecimals)) <= 0 ||
            scale.X <= 0 ||
            scale.Y <= 0) return false;

        return OsbSprite.InScreenBounds(
            new(noGen ? Position.X : float.Round(Position.X, generator.PositionDecimals),
                noGen ? Position.Y : float.Round(Position.Y, generator.PositionDecimals)),
            imageSize * scale,
            noGen ? Rotation : float.Round(Rotation, generator.RotationDecimals),
            origin);
    }
}