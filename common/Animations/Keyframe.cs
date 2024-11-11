namespace StorybrewCommon.Animations;

using System;
using System.Collections.Generic;

/// <summary> Represents a keyframe with a time and value. </summary>
/// <typeparam name="TValue"> A type that represents the value of this keyframe. </typeparam>
public readonly struct Keyframe<TValue>(float time, TValue value, Func<double, double> easing, bool until)
    : IEquatable<Keyframe<TValue>>, IComparer<Keyframe<TValue>>
{
    ///<summary> Time of this keyframe. </summary>
    public readonly float Time = time;

    ///<summary> Value of this keyframe. </summary>
    public readonly TValue Value = value;

    /// <summary> <see cref="EasingFunctions"/> easing of this keyframe. </summary>
    public readonly Func<double, double> Ease = easing ?? EasingFunctions.Linear;

    /// <summary> Reserved for <see cref="Storyboarding.Util.CommandGenerator"/>. </summary>
    internal readonly bool Until = until;

    /// <summary> Initializes a new keyframe. </summary>
    /// <param name="time"> Time of the keyframe. </param>
    /// <param name="value"> A value to be assigned to the keyframe. </param>
    /// <param name="easing"> <see cref="EasingFunctions"/> easing to be assigned. </param>
    public Keyframe(float time, TValue value = default, Func<double, double> easing = null) : this(time, value, easing, false) { }

    internal static readonly Comparer<Keyframe<TValue>> Comparer =
        Comparer<Keyframe<TValue>>.Create((x, y) => Math.Sign(x.Time - y.Time));

    ///<summary> Overrides a keyframe's time. </summary>
    public Keyframe<TValue> WithTime(float time) => new(time, Value, Ease);

    ///<summary> Overrides a keyframe's value. </summary>
    public Keyframe<TValue> WithValue(TValue value) => new(Time, value, Ease);

    /// <summary> Compares a keyframe to another keyframe of the same type for equality. </summary>
    /// <param name="other"> The other keyframe to be compared. </param>
    /// <returns> A value indicating whether both keyframes are equal. </returns>
    public bool Equals(Keyframe<TValue> other) => Time == other.Time && Value.Equals(other.Value);

    int IComparer<Keyframe<TValue>>.Compare(Keyframe<TValue> x, Keyframe<TValue> y) => Comparer.Compare(x, y);

    /// <summary> Creates a formatted string with this <see cref="Keyframe{TValue}"/>'s time and value. </summary>
    public override string ToString() => $"{Time:0.000}s {typeof(TValue)}:{Value}";
}