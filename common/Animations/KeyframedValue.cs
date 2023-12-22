using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Util;

namespace StorybrewCommon.Animations;

///<summary> Defines a set of keyframes which can be converted to commands. </summary>
///<typeparam name="TValue"> The type of values of the keyframed value. </typeparam>
///<param name="interpolate"> The <see cref="InterpolatingFunctions"/> to use to interpolate between values. Required to use <see cref="ValueAt(double)"/> </param>
///<param name="defaultValue"> The default value of the type of this keyframed value. </param>
public class KeyframedValue<TValue>(Func<TValue, TValue, double, TValue> interpolate = null, TValue defaultValue = default) : IEnumerable<Keyframe<TValue>>
{
    List<Keyframe<TValue>> keyframes = [];

    ///<summary> Returns the start time of the first keyframe in the keyframed value. </summary>
    public double StartTime => keyframes.Count == 0 ? int.MaxValue : keyframes[0].Time;

    ///<summary> Returns the end time of the last keyframe in the keyframed value. </summary>
    public double EndTime => keyframes.Count == 0 ? int.MinValue : keyframes[^1].Time;

    ///<summary> Returns the start value of the first keyframe in the keyframed value. </summary>
    public TValue StartValue => keyframes.Count == 0 ? defaultValue : keyframes[0].Value;

    ///<summary> Returns the end value of the last keyframe in the keyframed value. </summary>
    public TValue EndValue => keyframes.Count == 0 ? defaultValue : keyframes[^1].Value;

    ///<summary> Gets or sets the <see cref="Keyframe{TValue}"/> at the current index. </summary>
    public Keyframe<TValue> this[int index]
    {
        get => keyframes[index];
        set => keyframes[index] = value;
    }

    ///<summary> Returns the amount of keyframes in the keyframed value. </summary>
    public int Count => keyframes.Count;

    ///<summary> Adds a <see cref="Keyframe{TValue}"/> to the <see cref="List{T}"/> of keyframed values. </summary>
    ///<param name="keyframe"> The <see cref="Keyframe{TValue}"/> to be added. </param>
    ///<param name="before"> If a <see cref="Keyframe{TValue}"/> exists at this time, places new one before existing one. </param>
    public KeyframedValue<TValue> Add(Keyframe<TValue> keyframe, bool before = false)
    {
        if (keyframes.Count == 0 || keyframes[^1].Time < keyframe.Time) keyframes.Add(keyframe);
        else keyframes.Insert(indexFor(keyframe, before), keyframe);
        return this;
    }

    ///<summary> Adds an array or arrays to the keyframed value. </summary>
    ///<param name="values"> The array of keyframes. </param>
    public KeyframedValue<TValue> Add(params Keyframe<TValue>[] values) => AddRange(values);

    ///<summary> Adds a manually constructed keyframe to the keyframed value. </summary>
    ///<param name="time"> The time of the <see cref="Keyframe{TValue}"/>. </param>
    ///<param name="value"> The type value of the <see cref="Keyframe{TValue}"/>. </param>
    ///<param name="before"> If a <see cref="Keyframe{TValue}"/> exists at this time, places new one before existing one. </param>
    public KeyframedValue<TValue> Add(double time, TValue value, bool before = false) => Add(new(time, value), before);

    ///<summary> Adds a manually constructed keyframe to the keyframed value. </summary>
    ///<param name="time"> The time of the <see cref="Keyframe{TValue}"/>. </param>
    ///<param name="value"> The type value of the <see cref="Keyframe{TValue}"/>. </param>
    ///<param name="easing"> The <see cref="EasingFunctions"/> type of this <see cref="Keyframe{TValue}"/>. </param>
    ///<param name="before"> If a <see cref="Keyframe{TValue}"/> exists at this time, places new one before existing one. </param>
    public KeyframedValue<TValue> Add(double time, TValue value, Func<double, double> easing, bool before = false) => Add(new(time, value, easing), before);

    ///<summary> Adds a collection of keyframes to the keyframed value. </summary>
    public KeyframedValue<TValue> AddRange(IEnumerable<Keyframe<TValue>> collection)
    {
        foreach (var keyframe in collection) Add(keyframe);
        return this;
    }

    ///<summary> Adds a manually constructed keyframe to the keyframed value. Assumes the type value of this keyframe. </summary>
    ///<param name="time"> The time of the <see cref="Keyframe{TValue}"/>. </param>
    public KeyframedValue<TValue> Add(double time) => Add(time, ValueAt(time));

    ///<summary> Creates a wait period starting at the end of the previous keyframe until the given time. </summary>
    ///<param name="time"> The end time of the wait period. </param>
    public KeyframedValue<TValue> Until(double time)
    {
        if (keyframes.Count == 0) return null;
        return Add(time, EndValue);
    }

    internal KeyframedValue<TValue> DebugUntil(double time) => Add(new Keyframe<TValue>(time, EndValue, null, true));

    ///<summary> Transfers the keyframes in this instance to another keyframed value. </summary>
    ///<param name="to"> The keyframed value to transfer to. </param>
    ///<param name="clear"> Whether to clear the keyframes in this instance. </param>
    public void TransferKeyframes(KeyframedValue<TValue> to, bool clear = true)
    {
        if (Count == 0) return;
        to.AddRange(keyframes);
        if (clear) Clear();
    }

    ///<summary> Returns the value of the keyframed value at <paramref name="time"/>. </summary>
    public TValue ValueAt(double time)
    {
        var count = keyframes.Count;
        if (count == 0) return defaultValue;
        if (count == 1) return keyframes[0].Value;

        var index = indexAt(time, false);
        if (index == 0) return keyframes[0].Value;
        else if (index == count) return keyframes[count - 1].Value;
        else
        {
            var from = keyframes[index - 1];
            var to = keyframes[index];
            if (from.Time == to.Time) return to.Value;

            var progress = to.Ease((time - from.Time) / (to.Time - from.Time));
            return interpolate(from.Value, to.Value, progress);
        }
    }

    ///<summary> Converts keyframes to commands. </summary>
    ///<param name="pair"> A function to utilize the start and end keyframe of a pair. </param>
    ///<param name="defaultValue"> Pairs with this default value are skipped. </param>
    ///<param name="edit"> A function that edits each keyframe before being paired. </param>
    ///<param name="explicitStartTime"> The explicit start time for the keyframe set to pair. </param>
    ///<param name="explicitEndTime"> The explicit end time for the keyframed set to pair. </param>
    ///<param name="loopable"> Enable if <paramref name="pair"/> is encapsulated in or uses a trigger/loop group. </param>
    public void ForEachPair(Action<Keyframe<TValue>, Keyframe<TValue>> pair,
        TValue defaultValue = default, Func<TValue, TValue> edit = null,
        double? explicitStartTime = null, double? explicitEndTime = null, bool loopable = false)
    {
        if (keyframes.Count == 0) return;

        var startTime = explicitStartTime ?? keyframes[0].Time;
        var endTime = explicitEndTime ?? keyframes[^1].Time;

        bool hasPair = false, forceNextFlat = loopable;
        Keyframe<TValue>? previous = null, stepStart = null, previousPairEnd = null;

        for (var i = 0; i < keyframes.Count; ++i)
        {
            var endKeyframe = editKeyframe(keyframes[i], edit);
            if (previous.HasValue)
            {
                var startKeyframe = previous.Value;

                var isFlat = startKeyframe.Value.Equals(endKeyframe.Value);
                var isStep = !isFlat && startKeyframe.Time == endKeyframe.Time;

                if (isStep)
                {
                    if (!stepStart.HasValue) stepStart = startKeyframe;
                }
                else if (stepStart.HasValue)
                {
                    if (!hasPair && explicitStartTime.HasValue && startTime < stepStart.Value.Time && !stepStart.Value.Until)
                    {
                        var initialPair = stepStart.Value.WithTime(startTime);
                        pair(initialPair, loopable ? stepStart.Value : initialPair);
                    }

                    if (!stepStart.Value.Until) pair(stepStart.Value, startKeyframe);
                    previousPairEnd = startKeyframe;
                    stepStart = null;
                    hasPair = true;
                }
                if (!isStep && (!isFlat || forceNextFlat))
                {
                    if (!hasPair && explicitStartTime.HasValue && startTime < startKeyframe.Time)
                    {
                        var initialPair = startKeyframe.WithTime(startTime);
                        pair(initialPair, loopable ? startKeyframe : initialPair);
                    }

                    pair(startKeyframe, endKeyframe);
                    previousPairEnd = endKeyframe;
                    hasPair = true;
                    forceNextFlat = false;
                }
            }
            previous = endKeyframe;
        }
        if (stepStart.HasValue)
        {
            if (!hasPair && explicitStartTime.HasValue && startTime < stepStart.Value.Time)
            {
                var initialPair = stepStart.Value.WithTime(startTime);
                pair(loopable && previousPairEnd.HasValue ? previousPairEnd.Value : initialPair, initialPair);
            }

            pair(stepStart.Value, previous.Value);
            previousPairEnd = previous.Value;
            stepStart = null;
            hasPair = true;
        }
        if (!hasPair && keyframes.Count != 0)
        {
            var first = editKeyframe(keyframes[0], edit).WithTime(startTime);
            if (!first.Value.Equals(defaultValue))
            {
                var last = loopable ? first.WithTime(endTime) : first;
                pair(first, last);
                previousPairEnd = last;
                hasPair = true;
            }
        }
        if (hasPair && explicitEndTime.HasValue && previousPairEnd.Value.Time < endTime)
        {
            var endPair = previousPairEnd.Value.WithTime(endTime);
            pair(loopable ? previousPairEnd.Value : endPair, endPair);
        }
    }

    static Keyframe<TValue> editKeyframe(Keyframe<TValue> keyframe, Func<TValue, TValue> edit = null) => edit is not null ?
        new(keyframe.Time, edit(keyframe.Value), keyframe.Ease, keyframe.Until) : keyframe;

    ///<summary> Removes all keyframes in the keyframed value. </summary>
    public void Clear(bool trim = false)
    {
        if (keyframes.Count == 0) return;

        keyframes.Clear();
        if (trim) keyframes.Capacity = 0;
    }

    ///<summary> Returns an enumerator that iterates through the keyframed value. </summary>
    public IEnumerator<Keyframe<TValue>> GetEnumerator() => keyframes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => keyframes.GetEnumerator();

    int indexFor(Keyframe<TValue> keyframe, bool before)
    {
        var i = keyframes.BinarySearch(keyframe, new Keyframe<TValue>());
        if (i >= 0)
        {
            if (before) while (i > 0 && keyframes[i].Time >= keyframe.Time) --i;
            else while (i < keyframes.Count && keyframes[i].Time <= keyframe.Time) ++i;
        }
        else i = ~i;
        return i;
    }
    internal int indexAt(double time, bool before) => indexFor(new(time), before);

    #region Manipulation

    ///<summary> Simplifies keyframes with 1-parameter values. </summary>
    ///<param name="tolerance"> Distance threshold from which keyframes can be removed.  </param>
    ///<param name="getComponent"> Converts the keyframe values to a canonical <see cref="float"/> that the method can use. </param>
    public void Simplify1dKeyframes(double tolerance, Func<TValue, float> getComponent) => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
    {
        Vector2 start = new((float)startKeyframe.Time, getComponent(startKeyframe.Value)),
            middle = new((float)middleKeyframe.Time, getComponent(middleKeyframe.Value)),
            end = new((float)endKeyframe.Time, getComponent(endKeyframe.Value));

        Vector2 startToMiddle = middle - start, startToEnd = end - start;
        return (startToMiddle - Vector2.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd).LengthSquared();
    });

    ///<summary> Simplifies keyframes with 2-parameter values. </summary>
    ///<param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
    ///<param name="getComponent"> Converts the keyframe values to a canonical <see cref="Vector2"/> that the method can use. </param>
    public void Simplify2dKeyframes(double tolerance, Func<TValue, Vector2> getComponent) => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
    {
        Vector2 startComponent = getComponent(startKeyframe.Value), middleComponent = getComponent(middleKeyframe.Value), endComponent = getComponent(endKeyframe.Value);
        Vector3 start = new((float)startKeyframe.Time, startComponent.X, startComponent.Y),
            middle = new((float)middleKeyframe.Time, middleComponent.X, middleComponent.Y),
            end = new((float)endKeyframe.Time, endComponent.X, endComponent.Y);

        Vector3 startToMiddle = middle - start, startToEnd = end - start;
        return (startToMiddle - Vector3.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd).LengthSquared();
    });

    ///<summary> Simplifies keyframes with 3-parameter values. </summary>
    ///<param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
    ///<param name="getComponent"> Converts the keyframe values to a canonical <see cref="Vector3"/> that the method can use. </param>
    public void Simplify3dKeyframes(double tolerance, Func<TValue, Vector3> getComponent) => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
    {
        Vector3 startComponent = getComponent(startKeyframe.Value), middleComponent = getComponent(middleKeyframe.Value), endComponent = getComponent(endKeyframe.Value);
        Vector4 start = new((float)startKeyframe.Time, startComponent.X, startComponent.Y, startComponent.Z),
            middle = new((float)middleKeyframe.Time, middleComponent.X, middleComponent.Y, middleComponent.Z),
            end = new((float)endKeyframe.Time, endComponent.X, endComponent.Y, endComponent.Z);

        Vector4 startToMiddle = middle - start, startToEnd = end - start;
        return (startToMiddle - Vector4.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd).LengthSquared();
    });

    void SimplifyEqualKeyframes()
    {
        List<Keyframe<TValue>> simplifiedKeyframes = [];
        for (int i = 0, count = keyframes.Count; i < count; i++)
        {
            var startKeyframe = keyframes[i];
            simplifiedKeyframes.Add(startKeyframe);

            for (var j = i + 1; j < count; j++)
            {
                var endKeyframe = keyframes[j];
                if (!startKeyframe.Value.Equals(endKeyframe.Value))
                {
                    if (i < j - 1) simplifiedKeyframes.Add(keyframes[j - 1]);
                    simplifiedKeyframes.Add(endKeyframe);
                    i = j;
                    break;
                }
                else if (j == count - 1) i = j;
            }
        }

        Clear();
        keyframes = simplifiedKeyframes;
    }

    ///<summary> Simplifies keyframes on commands. </summary>
    ///<param name="tolerance"> Distance threshold (epsilon) from which keyframes can be removed. </param>
    ///<param name="getDistanceSq"> A function that gets the distance, squared, between three specific keyframes. </param>
    public void SimplifyKeyframes(double tolerance, Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistanceSq)
    {
        if (tolerance <= .00001)
        {
            SimplifyEqualKeyframes();
            return;
        }
        if (keyframes.Count < 3) return;

        var firstPoint = 0;
        var lastPoint = keyframes.Count - 1;

        List<int> keep = [firstPoint, lastPoint];
        getSimplifiedKeyframeIndexes(ref keep, firstPoint, lastPoint, tolerance * tolerance, getDistanceSq);
        if (keep.Count == keyframes.Count) return;

        List<Keyframe<TValue>> simplifiedKeyframes = new(keep.Count);
        keep.Sort();
        for (var i = 0; i < keep.Count; ++i) simplifiedKeyframes.Add(keyframes[keep[i]]);

        Clear();
        keyframes = simplifiedKeyframes;
    }
    void getSimplifiedKeyframeIndexes(ref List<int> keep, int first, int last, double epsilonSq, Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistance)
    {
        var start = keyframes[first];
        var end = keyframes[last];

        var maxDistSq = 0f;
        var indexFar = 0;

        for (var i = first; i < last; ++i)
        {
            var distanceSq = getDistance(start, keyframes[i], end);
            if (distanceSq > maxDistSq)
            {
                maxDistSq = distanceSq;
                indexFar = i;
            }
        }
        if (maxDistSq > epsilonSq && indexFar > 0)
        {
            getSimplifiedKeyframeIndexes(ref keep, first, indexFar, epsilonSq, getDistance);
            keep.Add(indexFar);
            getSimplifiedKeyframeIndexes(ref keep, indexFar, last, epsilonSq, getDistance);
        }
    }

    #endregion
}