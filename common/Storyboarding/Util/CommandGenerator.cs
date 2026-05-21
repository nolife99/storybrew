namespace StorybrewCommon.Storyboarding.Util;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Animations;
using BrewLib.Memory;
using Commands;
using CommandValues;
using Scripting;
using SixLabors.ImageSharp;

/// <summary> Generates commands on an <see cref="OsbSprite"/> based on the states of that sprite. </summary>
public class CommandGenerator
{
    static readonly ConditionalWeakTable<StoryboardObjectGenerator, Dictionary<int, Vector2>> dimensionTable = [];

    readonly KeyframedValue<CommandColor> colors = new(CommandColor.Lerp), finalColors = new(CommandColor.Lerp);

    readonly KeyframedValue<bool> flipH = new(InterpolatingFunctions.BoolFrom),
        flipV = new(InterpolatingFunctions.BoolFrom), additive = new(InterpolatingFunctions.BoolFrom);

    readonly KeyframedValue<CommandPosition> positions = new(CommandPosition.Lerp),
        finalPositions = new(CommandPosition.Lerp);

    readonly KeyframedValue<float> rotations = new(InterpolatingFunctions.FloatAngle), fades = new(float.Lerp),
        finalRotations = new(InterpolatingFunctions.FloatAngle), finalFades = new(float.Lerp);

    readonly KeyframedValue<CommandScale> scales = new(CommandScale.Lerp), finalScales = new(CommandScale.Lerp);

    PoolingMemoryStream states;

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
    public ref readonly State StartState
        => ref MemoryMarshal.Cast<byte, State>(states.WrittenSpan).GetPinnableReference();

    /// <summary> Gets the <see cref="CommandGenerator"/>'s end state. </summary>
    /// <remarks> If there are no states, returns a null reference. It is up to the caller to check for this. </remarks>
    public ref readonly State EndState
        => ref (states?.Length ?? 0) == 0 ?
            ref Unsafe.NullRef<State>() :
            ref MemoryMarshal.Cast<byte, State>(states.WrittenSpan)[^1];

    /// <summary> Adds a <see cref="State"/> to this instance that will be automatically sorted. </summary>
    public void Add(State state)
    {
        states ??= new();

        var span = MemoryMarshal.Cast<byte, State>(states.WrittenSpan);
        var count = span.Length;

        if (count == 0 || span[count - 1].Time <= state.Time)
        {
            states.Write(MemoryMarshal.AsBytes<State>(new(ref state)));
            return;
        }

        var i = span.BinarySearch(state, State.Comparer);
        if (i >= 0)
            while (i < count - 1 && span[i + 1].Time <= state.Time)
                ++i;
        else i = ~i;

        states.Write(MemoryMarshal.AsBytes<State>(new(ref state)));

        var newSpan = MemoryMarshal.CreateSpan(
            ref Unsafe.As<byte, State>(ref Unsafe.AsRef(in states.WrittenSpan.GetPinnableReference())),
            count + 1);

        newSpan.Slice(i, span.Length - i).CopyTo(newSpan[(i + 1)..]);
        newSpan[i] = state;
    }

    /// <summary> Generates commands on a sprite based on this generator's states. </summary>
    /// <param name="sprite"> The <see cref="OsbSprite"/> to have commands generated on. </param>
    /// <param name="action"> Encapsulates a group of commands to be generated on <paramref name="sprite"/>. </param>
    /// <param name="startTime">
    /// The explicit start time of the command generation. Can be left <see langword="null"/> if
    /// <see cref="State.Time"/> is used.
    /// </param>
    /// <param name="endTime">
    /// The explicit end time of the command generation. Can be left <see langword="null"/> if
    /// <see cref="State.Time"/> is used.
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
        if ((states?.Length ?? 0) == 0) return;

        ref readonly var previousState = ref Unsafe.NullRef<State>();
        bool wasVisible = false, everVisible = false, stateAdded = false;
        var imageSize = BitmapDimensions(sprite.TexturePath);

        var span = MemoryMarshal.Cast<byte, State>(states.WrittenSpan);
        ensureKeyframes(span.Length);

        foreach (ref readonly var state in span)
        {
            var time = state.Time + timeOffset;
            if (sprite is OsbAnimation) imageSize = BitmapDimensions(sprite.GetTexturePathAt(time));
            var isVisible = state.IsVisible(imageSize, sprite.Origin, this);

            if (isVisible && !everVisible) everVisible = true;
            switch (wasVisible)
            {
                case false when isVisible:
                    if (!stateAdded && !Unsafe.IsNullRef(in previousState))
                        addKeyframes(in previousState, loopable ? time : previousState.Time + timeOffset);

                    addKeyframes(in state, time);
                    if (!stateAdded) stateAdded = true;
                    break;

                case true when !isVisible:
                    addKeyframes(in state, time);
                    commitKeyframes(imageSize);
                    break;

                default:
                    if (isVisible) addKeyframes(in state, time);
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

    void convertToCommands(OsbSprite sprite,
        float? startTime,
        float? endTime,
        float timeOffset,
        Vector2 imageSize,
        bool loopable)
    {
        float? startState = loopable ? (startTime ?? StartState.Time) + timeOffset : null,
            endState = loopable ? (endTime ?? EndState.Time) + timeOffset : null;

        var moveX = finalPositions.keyframes.TrueForAll(keyframe => checkPos(keyframe.Value.Y) ==
            checkPos(finalPositions.StartValue.Y));

        var moveY = finalPositions.keyframes.TrueForAll(keyframe => checkPos(keyframe.Value.X) ==
            checkPos(finalPositions.StartValue.X));

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

        var scalar = true;
        foreach (var keyframe in finalScales.keyframes)
        {
            if (Math.Abs(checkScale(keyframe.Value.X) - checkScale(keyframe.Value.Y)) < 1) continue;

            scalar = false;
            break;
        }

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
                if (!(s.Time == sprite.StartTime && s.Time == e.Time && e.Value >= 1 || s.Time == sprite.EndTime ||
                    s.Time == EndState.Time && s.Time == e.Time && e.Value <= 0))
                    sprite.Fade(s.Time, e.Time, s.Value, e.Value);
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

    void addKeyframes(scoped ref readonly State state, float time)
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

    void ensureKeyframes(int stateCount)
    {
        positions.keyframes.EnsureCapacity(positions.keyframes.Count + stateCount);
        scales.keyframes.EnsureCapacity(scales.keyframes.Count + stateCount);
        rotations.keyframes.EnsureCapacity(rotations.keyframes.Count + stateCount);
        colors.keyframes.EnsureCapacity(colors.keyframes.Count + stateCount);
        fades.keyframes.EnsureCapacity(fades.keyframes.Count + stateCount);
        flipH.keyframes.EnsureCapacity(flipH.keyframes.Count + stateCount);
        flipV.keyframes.EnsureCapacity(flipV.keyframes.Count + stateCount);
        additive.keyframes.EnsureCapacity(additive.keyframes.Count + stateCount);
    }

    void clearKeyframes()
    {
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

        states.Dispose();
    }

    internal static Vector2 BitmapDimensions(string path)
    {
        var currentGen = StoryboardObjectGenerator.Current;

        ref var dimension = ref CollectionsMarshal.GetValueRefOrAddDefault(dimensionTable.GetOrCreateValue(currentGen),
            Path.GetFullPath(Path.Combine(currentGen.MapsetPath, path)).GetHashCode(StringComparison.OrdinalIgnoreCase),
            out var exists);

        if (exists) return dimension;

        using var stream = currentGen.OpenMapsetFile(path, false);
        var info = Image.Identify(stream);

        return dimension = new(info.Width, info.Height);
    }
}

/// <summary> Defines all of an <see cref="OsbSprite"/>'s states as a class. </summary>
public record struct State
{
    internal static readonly Comparer<State> Comparer = Comparer<State>.Create((a, b) => a.Time.CompareTo(b.Time));

    ///<summary> Represents the color, in RGB values, of this state. </summary>
    public CommandColor Color;

    byte flags;

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

    /// <summary> Creates a default state. </summary>
    public State()
    {
        Additive = FlipH = FlipV = false;
        Opacity = Rotation = Time = 0;
        Position = new(320, 240);
        Scale = CommandScale.One;
        Color = CommandColor.White;
    }

    ///<summary> Represents the additive toggle condition of this state. </summary>
    public bool Additive { get => (flags & 1) != 0; set => flags = (byte)(value ? flags | 1 : flags & ~1); }

    ///<summary> Represents the horizontal flip condition of this state. </summary>
    public bool FlipH { get => (flags & 2) != 0; set => flags = (byte)(value ? flags | 2 : flags & ~2); }

    ///<summary> Represents the vertical flip condition of this state. </summary>
    public bool FlipV { get => (flags & 4) != 0; set => flags = (byte)(value ? flags | 4 : flags & ~4); }

    /// <summary>
    /// Determines the visibility of the sprite in the current <see cref="State"/> based on its image dimensions and
    /// <see cref="OsbOrigin"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the sprite is visible within widescreen boundaries, else returns <see langword="false"/>
    /// .
    /// </returns>
    public bool IsVisible(Vector2 imageSize, OsbOrigin origin, CommandGenerator generator = null)
    {
        var noGen = generator is null;
        Vector2 scale = new(noGen ? Scale.X : float.Round(Scale.X, generator.ScaleDecimals),
            noGen ? Scale.Y : float.Round(Scale.Y, generator.ScaleDecimals));

        return Additive && Color == CommandColor.Black ||
            (noGen ? Opacity : float.Round(Opacity, generator.OpacityDecimals)) <= 0 || scale.X <= 0 || scale.Y <= 0 ?
                false :
                OsbSprite.InScreenBounds(
                    new(noGen ? Position.X : double.Round(Position.X, generator.PositionDecimals),
                        noGen ? Position.Y : double.Round(Position.Y, generator.PositionDecimals)),
                    imageSize * scale,
                    noGen ? Rotation : float.Round(Rotation, generator.RotationDecimals),
                    origin);
    }
}