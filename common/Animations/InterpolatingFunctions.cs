namespace StorybrewCommon.Animations;

using System;
using System.Numerics;
using BrewLib.Util;
using Storyboarding.CommandValues;

/// <summary> A static class providing interpolating functions. </summary>
public static class InterpolatingFunctions
{
    /// <summary> Interpolates between 2 <see cref="System.Numerics.Vector2"/> vectors. </summary>
    public static readonly Func<Vector2, Vector2, float, Vector2> Vector2 = System.Numerics.Vector2.Lerp;

    /// <summary> Interpolates between 2 <see cref="System.Numerics.Vector3"/> vectors. </summary>
    public static readonly Func<Vector3, Vector3, float, Vector3> Vector3 = System.Numerics.Vector3.Lerp;

    /// <summary> Performs spherical linear interpolation on 2 <see cref="Quaternion"/>s. </summary>
    public static readonly Func<Quaternion, Quaternion, float, Quaternion> QuaternionSlerp = Quaternion.Slerp;

    ///<summary> Returns the first value. </summary>
    public static readonly Func<bool, bool, float, bool> BoolFrom = (from, _, _) => from;

    ///<summary> Returns the second value. </summary>
    public static readonly Func<bool, bool, float, bool> BoolTo = (_, to, _) => to;

    ///<summary> Returns the OR comparison between the two values. </summary>
    public static readonly Func<bool, bool, float, bool> BoolAny = (from, to, _) => from || to;

    ///<summary> Returns the AND comparison between the two values. </summary>
    public static readonly Func<bool, bool, float, bool> BoolBoth = (from, to, _) => from && to;

    /// <summary> Interpolates between 2 <see cref="float"/>s. </summary>
    public static float Float(float from, float to, float progress) => from + (to - from) * progress;

    /// <summary> Interpolates between 2 <see cref="float"/> angles. </summary>
    public static float FloatAngle(float from, float to, float progress)
        => from + MathUtil.ShortestAngleDelta(from, to) * progress;

    /// <summary> Interpolates between 2 <see cref="double"/>s. </summary>
    public static double Double(double from, double to, float progress) => from + (to - from) * progress;

    /// <summary> Interpolates between 2 <see cref="double"/> angles. </summary>
    public static double DoubleAngle(double from, double to, float progress)
        => from + MathUtil.ShortestAngleDelta(from, to) * progress;

    /// <summary> Interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
    public static CommandPosition Position(CommandPosition from, CommandPosition to, float progress)
        => Vector2(from, to, progress);

    /// <summary> Interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
    public static CommandScale Scale(CommandScale from, CommandScale to, float progress) => Vector2(from, to, progress);

    /// <summary> Interpolates between 2 <see cref="Storyboarding.CommandValues.CommandColor"/> RGB values. </summary>
    public static CommandColor CommandColor(CommandColor from, CommandColor to, float progress) => from + (to - from) * progress;
}