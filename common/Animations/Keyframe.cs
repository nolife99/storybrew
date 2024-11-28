namespace StorybrewCommon.Animations;

using System;
using System.Collections.Generic;

/// <summary>
///     Represents a point in time and a value in a keyframe collection.
/// </summary>
/// <typeparam name="TValue"> The type of the value of the keyframe. </typeparam>
/// <remarks>
///     This structure is used in conjunction with <see cref="KeyframedValue{TValue}"/> to represent values that change
///     over time.
/// </remarks>
public readonly record struct Keyframe<TValue> : IComparer<Keyframe<TValue>>
{
    internal static readonly Comparer<Keyframe<TValue>> Comparer =
        Comparer<Keyframe<TValue>>.Create((x, y) => Math.Sign(x.Time - y.Time));

    /// <summary>
    ///     Gets the easing function to apply to this keyframe.
    /// </summary>
    /// <remarks>
    ///     This easing function is used when interpolating between this keyframe and the previous keyframe.
    /// </remarks>
    public readonly Func<float, float> Ease;

    /// <summary>
    ///     Gets the time of the keyframe.
    /// </summary>
    public readonly float Time;

    /// <summary> Reserved for <see cref="Storyboarding.Util.CommandGenerator"/>. </summary>
    internal readonly bool Until;

    /// <summary>
    ///     Gets the value of the keyframe.
    /// </summary>
    public readonly TValue Value;

    /// <summary>
    ///     Creates a new <see cref="Keyframe{TValue}"/> with the given time and optional value and easing function.
    /// </summary>
    /// <param name="time"> The time of the keyframe. </param>
    /// <param name="value"> The value of the keyframe. </param>
    /// <param name="easing"> The easing function to apply to this keyframe. </param>
    public Keyframe(float time, TValue value = default, Func<float, float> easing = null) : this(time, value, easing, false) { }

    internal Keyframe(float time, TValue value, Func<float, float> easing, bool until)
    {
        Time = time;
        Value = value;
        Ease = easing ?? EasingFunctions.Linear;
        Until = until;
    }

    int IComparer<Keyframe<TValue>>.Compare(Keyframe<TValue> x, Keyframe<TValue> y) => Comparer.Compare(x, y);

    /// <summary>
    ///     Creates a new <see cref="Keyframe{TValue}"/> with the same value and easing function as this one,
    ///     but with the given <paramref name="time"/>.
    /// </summary>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <returns> The new <see cref="Keyframe{TValue}"/>. </returns>
    public Keyframe<TValue> WithTime(float time) => new(time, Value, Ease);

    /// <summary>
    ///     Creates a new <see cref="Keyframe{TValue}"/> with the same time and easing function as this one,
    ///     but with the given <paramref name="value"/>.
    /// </summary>
    /// <param name="value"> The value of the new keyframe. </param>
    /// <returns> The new <see cref="Keyframe{TValue}"/>. </returns>
    public Keyframe<TValue> WithValue(TValue value) => new(Time, value, Ease);

    /// <inheritdoc/>
    public override string ToString() => $"{Time:0.000}s {typeof(TValue)}:{Value}";
}