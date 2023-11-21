using System;
using System.Collections.Generic;

namespace StorybrewCommon.Animations
{
    /// <summary> Represents a basic keyframe with a time and value. </summary>
    /// <typeparam name="TValue"> A type that represents the value of this keyframe. </typeparam>
    public readonly struct Keyframe<TValue> : IEquatable<Keyframe<TValue>>, IComparable<Keyframe<TValue>>, IComparer<Keyframe<TValue>>
    {
        ///<summary> Time of this keyframe. </summary>
        public readonly double Time;

        ///<summary> Type value of this keyframe. </summary>
        public readonly TValue Value;

        ///<summary> <see cref="EasingFunctions"/> type of this keyframe. </summary>
        public readonly Func<double, double> Ease;

        ///<summary> Reserved for <see cref="Storyboarding.Util.CommandGenerator"/>. Do not use. </summary>
        internal readonly bool Until;

        ///<summary> Initializes a new keyframe with default type value. </summary>
        ///<param name="time"> Time of the keyframe. </param>
        public Keyframe(double time) : this(time, default) { }

        ///<summary> Initializes a new keyframe. </summary>
        ///<param name="time"> Time of the keyframe. </param>
        ///<param name="value"> Any type value to be assigned to the keyframe. </param>
        ///<param name="easing"> An <see cref="EasingFunctions"/> type to be assigned. </param>
        public Keyframe(double time, TValue value, Func<double, double> easing = null) : this(time, value, easing, false) { }

        internal Keyframe(double time, TValue value, Func<double, double> easing, bool until)
        {
            Time = time;
            Value = value;
            Ease = easing ?? EasingFunctions.Linear;
            Until = until;
        }

        ///<summary> Overrides a keyframe with a new time. </summary>
        ///<param name="time"> The time to be overriden with. </param>
        public Keyframe<TValue> WithTime(double time) => new(time, Value, Ease);

        ///<summary> Overrides a keyframe with a new type value. </summary>
        ///<param name="value"> The type value to be overriden with. </param>
        public Keyframe<TValue> WithValue(TValue value) => new(Time, value, Ease);

        ///<summary> Compares a keyframe to another keyframe of the same type for equality. </summary>
        ///<param name="other"> The other keyframe to be compared. </param>
        ///<returns> A value indicating whether both keyframes are equal. </returns>
        public bool Equals(Keyframe<TValue> other) => Time == other.Time && Value.Equals(other.Value);

        ///<summary> Compares a keyframe to another keyframe of the same type. </summary>
        ///<param name="other"> The other keyframe to be compared. </param>
        ///<returns> A relative value of the comparison. </returns>
        public int CompareTo(Keyframe<TValue> other) => Math.Sign(Time - other.Time);
        int IComparer<Keyframe<TValue>>.Compare(Keyframe<TValue> x, Keyframe<TValue> y) => Math.Sign(x.Time - y.Time);

        ///<summary> Creates a formatted string containing this <see cref="Keyframe{TValue}"/>'s time and value. </summary>
        public override string ToString() => $"{Time:0.000}s {typeof(TValue)}:{Value}";
    }
}