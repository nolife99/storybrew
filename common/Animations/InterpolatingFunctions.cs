namespace StorybrewCommon.Animations;

using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.Util;
using Storyboarding.CommandValues;

/// <summary> A static class providing interpolating functions. </summary>
public static class InterpolatingFunctions
{
    /// <summary> Interpolates between 2 <see cref="System.Numerics.Vector2"/> vectors. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Vector2(Vector2 from, Vector2 to, float progress)
        => System.Numerics.Vector2.Lerp(from, to, progress);

    /// <summary> Interpolates between 2 <see cref="System.Numerics.Vector3"/> vectors. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Vector3(Vector3 from, Vector3 to, float progress)
        => System.Numerics.Vector3.Lerp(from, to, progress);

    /// <summary> Performs spherical linear interpolation on 2 <see cref="Quaternion"/>s. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion QuaternionSlerp(Quaternion from, Quaternion to, float progress)
        => Quaternion.Slerp(from, to, progress);

    ///<summary> Returns the first value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BoolFrom(bool from, bool to, float progress) => from;

    ///<summary> Returns the second value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BoolTo(bool from, bool to, float progress) => to;

    ///<summary> Returns the OR comparison between the two values. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BoolAny(bool from, bool to, float progress) => from || to;

    ///<summary> Returns the AND comparison between the two values. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BoolBoth(bool from, bool to, float progress) => from && to;

    /// <summary> Interpolates between 2 <see cref="float"/>s. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Float(float from, float to, float progress) => from + (to - from) * progress;

    /// <summary> Interpolates between 2 <see cref="float"/> angles. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float FloatAngle(float from, float to, float progress)
        => from + MathUtil.ShortestAngleDelta(from, to) * progress;

    /// <summary> Interpolates between 2 <see cref="double"/>s. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Double(double from, double to, float progress) => from + (to - from) * progress;

    /// <summary> Interpolates between 2 <see cref="double"/> angles. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DoubleAngle(double from, double to, float progress)
        => from + MathUtil.ShortestAngleDelta(from, to) * progress;

    /// <summary> Interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandPosition Position(CommandPosition from, CommandPosition to, float progress)
        => CommandPosition.Lerp(from, to, progress);

    /// <summary> Interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandScale Scale(CommandScale from, CommandScale to, float progress)
        => System.Numerics.Vector2.Lerp(from, to, progress);

    /// <summary> Interpolates between 2 <see cref="Storyboarding.CommandValues.CommandColor"/> RGB values. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor CommandColor(CommandColor from, CommandColor to, float progress)
        => from + (to - from) * progress;
}