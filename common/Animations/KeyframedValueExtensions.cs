namespace StorybrewCommon.Animations;

using System;
using System.Numerics;
using Storyboarding.CommandValues;

/// <summary>
///     Extension methods for <see cref="KeyframedValue{TValue}"/>.
/// </summary>
public static class KeyframedValueExtensions
{
    /// <summary>
    ///     Performs <paramref name="action"/> for each value in <paramref name="keyframes"/> that is true.
    /// </summary>
    public static void ForEachFlag(this KeyframedValue<bool> keyframes, Action<float, float> action)
    {
        var active = false;
        var startTime = 0f;
        var lastKeyframeTime = 0f;

        foreach (var keyframe in keyframes)
            if (keyframe.Value != active)
            {
                if (keyframe.Value)
                {
                    startTime = keyframe.Time;
                    active = true;
                }
                else
                {
                    action(startTime, keyframe.Time);
                    active = false;
                }
            }
            else lastKeyframeTime = keyframe.Time;

        if (active) action(startTime, lastKeyframeTime);
    }

    /// <summary> Adds a <see cref="CommandScale"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="x"> The <see cref="CommandScale.X"/> value of the keyframe. </param>
    /// <param name="y"> The <see cref="CommandScale.Y"/> value of the keyframe. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<CommandScale> Add(this KeyframedValue<CommandScale> keyframes,
        float time,
        double x,
        double y,
        Func<double, double> easing = null) => keyframes.Add(time, new(x, y), easing);

    /// <summary> Adds a <see cref="Vector2"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="scale"> The scale value of this <see cref="Keyframe{T}"/>. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<CommandScale> Add(this KeyframedValue<CommandScale> keyframes,
        float time,
        double scale,
        Func<double, double> easing = null) => keyframes.Add(time, new(scale), easing);

    /// <summary> Adds a <see cref="CommandPosition"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="x"> The <see cref="CommandPosition.X"/> value of the keyframe. </param>
    /// <param name="y"> The <see cref="CommandPosition.Y"/> value of the keyframe. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<CommandPosition> Add(this KeyframedValue<CommandPosition> keyframes,
        float time,
        double x,
        double y,
        Func<double, double> easing = null) => keyframes.Add(time, new(x, y), easing);

    /// <summary> Adds a <see cref="CommandPosition"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="xy"> The x and y value of this <see cref="Keyframe{T}"/>. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<CommandPosition> Add(this KeyframedValue<CommandPosition> keyframes,
        float time,
        double xy,
        Func<double, double> easing = null) => keyframes.Add(time, new(xy, xy), easing);

    /// <summary> Adds a <see cref="Vector2"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="x"> The <see cref="Vector2.X"/> value of the keyframe. </param>
    /// <param name="y"> The <see cref="Vector2.Y"/> value of the keyframe. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Vector2> Add(this KeyframedValue<Vector2> keyframes,
        float time,
        float x,
        float y,
        Func<double, double> easing = null) => keyframes.Add(time, new(x, y), easing);

    /// <summary> Adds a <see cref="Vector2"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="scale"> The scale value of this <see cref="Keyframe{T}"/>. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Vector2> Add(this KeyframedValue<Vector2> keyframes,
        float time,
        float scale,
        Func<double, double> easing = null) => keyframes.Add(time, new(scale), easing);

    /// <summary> Adds a <see cref="Vector3"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="x"> The <see cref="Vector3.X"/> value of the keyframe. </param>
    /// <param name="y"> The <see cref="Vector3.Y"/> value of the keyframe. </param>
    /// <param name="z"> The <see cref="Vector3.Z"/> value of the keyframe. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Vector3> Add(this KeyframedValue<Vector3> keyframes,
        float time,
        float x,
        float y,
        float z,
        Func<double, double> easing = null) => keyframes.Add(time, new(x, y, z), easing);

    /// <summary> Adds a <see cref="Vector3"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="scale"> The scale value of this <see cref="Keyframe{T}"/>. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Vector3> Add(this KeyframedValue<Vector3> keyframes,
        float time,
        float scale,
        Func<double, double> easing = null) => keyframes.Add(time, new(scale), easing);

    /// <summary> Adds a <see cref="Quaternion"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="axis"> The axis to rotate about. </param>
    /// <param name="angle"> The rotation angle in radians. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Quaternion> Add(this KeyframedValue<Quaternion> keyframes,
        float time,
        Vector3 axis,
        float angle,
        Func<double, double> easing = null)
    {
        var half = angle * .5f;
        var sin = MathF.Sin(half);
        return keyframes.Add(time, new(axis.X * sin, axis.Y * sin, axis.Z * sin, MathF.Cos(half)), easing);
    }

    /// <summary> Adds a <see cref="Quaternion"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="angle"> The rotation angle in radians (rotates about all axes). </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Quaternion> Add(this KeyframedValue<Quaternion> keyframes,
        float time,
        float angle,
        Func<double, double> easing = null)
        => keyframes.Add(time, Quaternion.CreateFromYawPitchRoll(angle, angle, angle), easing);

    /// <summary> Adds a <see cref="Quaternion"/> keyframe to <paramref name="keyframes"/>. </summary>
    /// <param name="keyframes"> The keyframed value to be added to. </param>
    /// <param name="time"> The time of the <see cref="Keyframe{T}"/>. </param>
    /// <param name="pitch"> The pitch (x-axis) angle of the <see cref="Quaternion"/>. </param>
    /// <param name="yaw"> The yaw (y-axis) angle of the <see cref="Quaternion"/>. </param>
    /// <param name="roll"> The roll (z-axis) angle of the <see cref="Quaternion"/>. </param>
    /// <param name="easing"> The <see cref="EasingFunctions"/> to apply to this <see cref="Keyframe{T}"/>. </param>
    public static KeyframedValue<Quaternion> Add(this KeyframedValue<Quaternion> keyframes,
        float time,
        float pitch,
        float yaw,
        float roll,
        Func<double, double> easing = null) => keyframes.Add(time, Quaternion.CreateFromYawPitchRoll(pitch, yaw, roll), easing);
}