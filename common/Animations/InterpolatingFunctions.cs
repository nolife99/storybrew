using System;
using System.Numerics;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Animations;

/// <summary> A static class providing interpolating functions. </summary>
public static class InterpolatingFunctions
{
    ///<summary> Interpolates between 2 <see cref="float"/>s. </summary>
    public static readonly Func<float, float, double, float> Float = (from, to, progress) => (float)(from + (to - from) * progress);

    ///<summary> Interpolates between 2 <see cref="float"/> angles. </summary>
    public static readonly Func<float, float, double, float> FloatAngle = (from, to, progress) => (float)(from + MathUtil.ShortestAngleDelta(from, to) * progress);

    ///<summary> Interpolates between 2 <see cref="double"/>s. </summary>
    public static readonly Func<double, double, double, double> Double = (from, to, progress) => from + (to - from) * progress;

    ///<summary> Interpolates between 2 <see cref="double"/> angles. </summary>
    public static readonly Func<double, double, double, double> DoubleAngle = (from, to, progress) => from + MathUtil.ShortestAngleDelta(from, to) * progress;

    ///<summary> Interpolates between 2 <see cref="System.Numerics.Vector2"/> vectors. </summary>
    public static readonly Func<Vector2, Vector2, double, Vector2> Vector2 = (from, to, progress) => System.Numerics.Vector2.Lerp(from, to, (float)progress);

    ///<summary> Interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
    public static readonly Func<CommandPosition, CommandPosition, double, CommandPosition> Position = (from, to, progress) => Vector2(from, to, progress);

    ///<summary> Interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
    public static readonly Func<CommandScale, CommandScale, double, CommandScale> Scale = (from, to, progress) => Vector2(from, to, progress);

    ///<summary> Interpolates between 2 <see cref="System.Numerics.Vector3"/> vectors. </summary>
    public static readonly Func<Vector3, Vector3, double, Vector3> Vector3 = (from, to, progress) => System.Numerics.Vector3.Lerp(from, to, (float)progress);

    ///<summary> Performs spherical linear interpolation on 2 <see cref="Quaternion"/>s. </summary>
    public static readonly Func<Quaternion, Quaternion, double, Quaternion> QuaternionSlerp = (from, to, progress) => Quaternion.Slerp(from, to, (float)progress);

    ///<summary> Returns the first value. </summary>
    public static readonly Func<bool, bool, double, bool> BoolFrom = (from, to, progress) => from;

    ///<summary> Returns the second value. </summary>
    public static readonly Func<bool, bool, double, bool> BoolTo = (from, to, progress) => to;

    ///<summary> Returns the OR comparison between the two values. </summary>
    public static readonly Func<bool, bool, double, bool> BoolAny = (from, to, progress) => from || to;

    ///<summary> Returns the AND comparison between the two values. </summary>
    public static readonly Func<bool, bool, double, bool> BoolBoth = (from, to, progress) => from && to;

    ///<summary> Interpolates between 2 <see cref="Storyboarding.CommandValues.CommandColor"/> RGB values. </summary>
    public static readonly Func<CommandColor, CommandColor, double, CommandColor> CommandColor = (from, to, progress) => from + (to - from) * progress;
}