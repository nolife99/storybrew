namespace StorybrewCommon.Animations;

using System;
using System.Numerics;
using Storyboarding.CommandValues;

/// <summary>Extension methods for <see cref="KeyframedValue{TValue}"/>.</summary>
public static class KeyframedValueExtensions
{
    /// <summary>
    ///     Iterates through each keyframe in <paramref name="keyframes"/>, calling <paramref name="action"/> for each true
    ///     value.
    /// </summary>
    public static void ForEachFlag(this KeyframedValue<bool> keyframes, Action<float, float> action)
    {
        var active = false;
        var startTime = 0f;
        var lastKeyframeTime = 0f;

        foreach (var keyframe in keyframes.keyframes)
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

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="x"> The x component of the scale to add. </param>
    /// <param name="y"> The y component of the scale to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<CommandScale> Add(this KeyframedValue<CommandScale> keyframes,
        float time,
        double x,
        double y,
        Func<float, float> easing = null) => keyframes.Add(time, new(x, y), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="scale"> The scale to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<CommandScale> Add(this KeyframedValue<CommandScale> keyframes,
        float time,
        double scale,
        Func<float, float> easing = null) => keyframes.Add(time, new(scale), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="x"> The x component of the position to add. </param>
    /// <param name="y"> The y component of the position to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<CommandPosition> Add(this KeyframedValue<CommandPosition> keyframes,
        float time,
        double x,
        double y,
        Func<float, float> easing = null) => keyframes.Add(time, new(x, y), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="xy"> The x and y components of the position to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<CommandPosition> Add(this KeyframedValue<CommandPosition> keyframes,
        float time,
        double xy,
        Func<float, float> easing = null) => keyframes.Add(time, new(xy, xy), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="x"> The x component of the vector to add. </param>
    /// <param name="y"> The y component of the vector to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Vector2> Add(this KeyframedValue<Vector2> keyframes,
        float time,
        float x,
        float y,
        Func<float, float> easing = null) => keyframes.Add(time, new(x, y), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="scale"> The scale to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Vector2> Add(this KeyframedValue<Vector2> keyframes,
        float time,
        float scale,
        Func<float, float> easing = null) => keyframes.Add(time, new(scale), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="x"> The x component of the vector to add. </param>
    /// <param name="y"> The y component of the vector to add. </param>
    /// <param name="z"> The z component of the vector to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Vector3> Add(this KeyframedValue<Vector3> keyframes,
        float time,
        float x,
        float y,
        float z,
        Func<float, float> easing = null) => keyframes.Add(time, new(x, y, z), easing);

    /// <summary>Adds a keyframe with the given value to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="scale"> The scale to add. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Vector3> Add(this KeyframedValue<Vector3> keyframes,
        float time,
        float scale,
        Func<float, float> easing = null) => keyframes.Add(time, new(scale), easing);

    /// <summary>Adds a keyframe with the given rotation to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="axis"> The axis of rotation. </param>
    /// <param name="angle"> The angle of rotation. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Quaternion> Add(this KeyframedValue<Quaternion> keyframes,
        float time,
        Vector3 axis,
        float angle,
        Func<float, float> easing = null)
    {
        var (sin, cos) = float.SinCos(angle * .5f);
        return keyframes.Add(time, new(axis.X * sin, axis.Y * sin, axis.Z * sin, cos), easing);
    }

    /// <summary>Adds a keyframe with the given rotation to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="angle"> The angle of rotation. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Quaternion> Add(this KeyframedValue<Quaternion> keyframes,
        float time,
        float angle,
        Func<float, float> easing = null) => keyframes.Add(
        time,
        Quaternion.CreateFromYawPitchRoll(angle, angle, angle),
        easing);

    /// <summary>Adds a keyframe with the given rotation to the keyframes.</summary>
    /// <param name="keyframes"> The keyframes to add to. </param>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="pitch"> The angle of rotation about the x axis. </param>
    /// <param name="yaw"> The angle of rotation about the y axis. </param>
    /// <param name="roll"> The angle of rotation about the z axis. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public static KeyframedValue<Quaternion> Add(this KeyframedValue<Quaternion> keyframes,
        float time,
        float pitch,
        float yaw,
        float roll,
        Func<float, float> easing = null)
        => keyframes.Add(time, Quaternion.CreateFromYawPitchRoll(pitch, yaw, roll), easing);
}