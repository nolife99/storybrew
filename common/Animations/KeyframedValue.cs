namespace StorybrewCommon.Animations;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

/// <summary> Defines a set of keyframes. </summary>
/// <typeparam name="TValue"> The type of values of the keyframes. </typeparam>
/// <param name="interpolate">
///     <see cref="InterpolatingFunctions" /> to use to interpolate between values. Required to use
///     <see cref="ValueAt(float)" />
/// </param>
/// <param name="defaultValue"> The default value of this keyframed value. </param>
public class KeyframedValue<TValue>(
    Func<TValue, TValue, float, TValue> interpolate = null, TValue defaultValue = default)
    : IEnumerable<Keyframe<TValue>>
{
    List<Keyframe<TValue>> keyframes = [];

    ///<summary> Returns the start time of the first keyframe. </summary>
    public float StartTime => keyframes.Count == 0 ? int.MaxValue : keyframes[0].Time;

    ///<summary> Returns the end time of the last keyframe. </summary>
    public float EndTime => keyframes.Count == 0 ? int.MinValue : keyframes[^1].Time;

    ///<summary> Returns the start value of the first keyframe. </summary>
    public TValue StartValue => keyframes.Count == 0 ? defaultValue : keyframes[0].Value;

    ///<summary> Returns the end value of the last keyframe. </summary>
    public TValue EndValue => keyframes.Count == 0 ? defaultValue : keyframes[^1].Value;

    /// <summary> Gets or sets the <see cref="Keyframe{TValue}" /> at the current index. </summary>
    public Keyframe<TValue> this[int index]
    {
        get => keyframes[index];
        set => keyframes[index] = value;
    }

    ///<summary> The amount of keyframes in the keyframed value. </summary>
    public int Count => keyframes.Count;

    ///<summary> Returns an enumerator that iterates through the set. </summary>
    public IEnumerator<Keyframe<TValue>> GetEnumerator() => keyframes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => keyframes.GetEnumerator();

    /// <summary> Adds a <see cref="Keyframe{TValue}" /> to the set. </summary>
    /// <param name="keyframe"> The <see cref="Keyframe{TValue}" /> to be added. </param>
    /// <param name="before"> If a <see cref="Keyframe{TValue}" /> exists at this time, inserted before existing one. </param>
    public KeyframedValue<TValue> Add(Keyframe<TValue> keyframe, bool before = false)
    {
        if (keyframes.Count == 0 || keyframes[^1].Time < keyframe.Time)
            keyframes.Add(keyframe);
        else
            keyframes.Insert(indexFor(keyframe, before), keyframe);
        return this;
    }

    ///<summary> Adds keyframes to the set. </summary>
    public KeyframedValue<TValue> Add(params Keyframe<TValue>[] values) => AddRange(values);

    /// <summary> Adds a keyframe to the set. </summary>
    /// <param name="time"> The time of the <see cref="Keyframe{TValue}" />. </param>
    /// <param name="value"> The value of the <see cref="Keyframe{TValue}" />. </param>
    /// <param name="before"> If a <see cref="Keyframe{TValue}" /> exists at this time, inserted before existing one. </param>
    public KeyframedValue<TValue> Add(float time, TValue value, bool before = false) => Add(new(time, value), before);

    /// <summary> Adds a keyframe to the keyframed value. </summary>
    /// <param name="time"> The time of the <see cref="Keyframe{TValue}" />. </param>
    /// <param name="value"> The value of the <see cref="Keyframe{TValue}" />. </param>
    /// <param name="easing"> The <see cref="EasingFunctions" /> type of this <see cref="Keyframe{TValue}" />. </param>
    /// <param name="before"> If a <see cref="Keyframe{TValue}" /> exists at this time, inserted before existing one. </param>
    public KeyframedValue<TValue> Add(float time, TValue value, Func<float, float> easing, bool before = false)
        => Add(new(time, value, easing), before);

    ///<summary> Adds keyframes to the set. </summary>
    public KeyframedValue<TValue> AddRange(IEnumerable<Keyframe<TValue>> collection)
    {
        foreach (var keyframe in collection) Add(keyframe);
        return this;
    }

    /// <summary> Adds a keyframe to the keyframed value. The value is determined through interpolation. </summary>
    /// <param name="time"> The time of the <see cref="Keyframe{TValue}" />. </param>
    public KeyframedValue<TValue> Add(float time) => Add(time, ValueAt(time));

    ///<summary> Waits from at the end of the previous keyframe until the given time. </summary>
    public KeyframedValue<TValue> Until(float time) => keyframes.Count == 0 ? null : Add(time, EndValue);

    internal KeyframedValue<TValue> DebugUntil(float time) => Add(new Keyframe<TValue>(time, EndValue, null, true));

    /// <summary> Transfers the keyframes in this instance. </summary>
    /// <param name="to"> The set to transfer to. </param>
    /// <param name="clear"> Whether to clear the keyframes in this instance. </param>
    public void TransferKeyframes(KeyframedValue<TValue> to, bool clear = true)
    {
        if (Count == 0) return;
        to.AddRange(keyframes);
        if (clear) Clear();
    }

    /// <summary> Returns the value at <paramref name="time" /> using interpolation. </summary>
    public TValue ValueAt(float time)
    {
        switch (keyframes.Count)
        {
            case 0: return defaultValue;
            case 1: return keyframes[0].Value;
        }

        var i = indexAt(time, false);
        if (i == 0) return keyframes[0].Value;
        if (i == keyframes.Count) return keyframes[^1].Value;
        var from = keyframes[i - 1];
        var to = keyframes[i];
        if (from.Time == to.Time) return to.Value;

        var progress = to.Ease((time - from.Time) / (to.Time - from.Time));
        return interpolate(from.Value, to.Value, progress);
    }

    /// <summary> Converts keyframes to commands. </summary>
    /// <param name="pair"> A function that takes the start and end keyframe of a pair. </param>
    /// <param name="defaultValue"> Pairs with this value are skipped. </param>
    /// <param name="edit"> A function that edits each keyframe before being paired. </param>
    /// <param name="explicitStartTime"> The explicit start time for first keyframe. </param>
    /// <param name="explicitEndTime"> The explicit end time for last keyframe. </param>
    /// <param name="loopable"> Enable if <paramref name="pair" /> is encapsulated in or uses a trigger/loop group. </param>
    public void ForEachPair(Action<Keyframe<TValue>, Keyframe<TValue>> pair, TValue defaultValue = default,
        Func<TValue, TValue> edit = null, float? explicitStartTime = null, float? explicitEndTime = null,
        bool loopable = false)
    {
        if (keyframes.Count == 0) return;

        var startTime = explicitStartTime ?? keyframes[0].Time;
        var endTime = explicitEndTime ?? keyframes[^1].Time;

        bool hasPair = false, forceNextFlat = loopable;
        Keyframe<TValue>? previous = null, stepStart = null, previousPairEnd = null;

        foreach (var t in keyframes)
        {
            var endKeyframe = editKeyframe(t, edit);
            if (previous.HasValue)
            {
                var startKeyframe = previous.Value;
                var isFlat = startKeyframe.Value.Equals(endKeyframe.Value);
                var isStep = !isFlat && startKeyframe.Time == endKeyframe.Time;

                if (isStep) stepStart ??= startKeyframe;
                else if (stepStart.HasValue)
                {
                    if (!hasPair && explicitStartTime.HasValue && startTime < stepStart.Value.Time &&
                        !stepStart.Value.Until)
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

        if (!hasPair || !explicitEndTime.HasValue || !(previousPairEnd.Value.Time < endTime)) return;

        var endPair = previousPairEnd.Value.WithTime(endTime);
        pair(loopable ? previousPairEnd.Value : endPair, endPair);
    }

    static Keyframe<TValue> editKeyframe(Keyframe<TValue> keyframe, Func<TValue, TValue> edit = null)
        => edit is not null ? new(keyframe.Time, edit(keyframe.Value), keyframe.Ease, keyframe.Until) : keyframe;

    ///<summary> Removes all keyframes in the set. </summary>
    public void Clear(bool trim = false)
    {
        if (keyframes.Count == 0) return;

        keyframes.Clear();
        if (trim) keyframes.Capacity = 0;
    }

    int indexFor(Keyframe<TValue> keyframe, bool before)
    {
        var i = keyframes.BinarySearch(keyframe, keyframe);
        if (i >= 0)
        {
            if (before)
                while (i > 0 && keyframes[i].Time >= keyframe.Time)
                    --i;
            else
                while (i < keyframes.Count && keyframes[i].Time <= keyframe.Time)
                    ++i;
        }
        else
            i = ~i;

        return i;
    }

    internal int indexAt(float time, bool before) => indexFor(new(time), before);

#region Manipulation

    /// <summary> Simplifies keyframes with 1-parameter values. </summary>
    /// <param name="tolerance"> Distance threshold from which keyframes can be removed.  </param>
    /// <param name="getComponent"> Converts the keyframe values to a canonical <see cref="float" /> that the method can use. </param>
    public void Simplify1dKeyframes(float tolerance, Func<TValue, float> getComponent)
        => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            Vector2 start = new(startKeyframe.Time, getComponent(startKeyframe.Value)),
                middle = new(middleKeyframe.Time, getComponent(middleKeyframe.Value)),
                end = new(endKeyframe.Time, getComponent(endKeyframe.Value));

            Vector2 startToMiddle = middle - start, startToEnd = end - start;
            return (startToMiddle - Vector2.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd)
                .LengthSquared();
        });

    /// <summary> Simplifies keyframes with 2-parameter values. </summary>
    /// <param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
    /// <param name="getComponent"> Converts the keyframe values to a canonical <see cref="Vector2" /> that the method can use. </param>
    public void Simplify2dKeyframes(float tolerance, Func<TValue, Vector2> getComponent)
        => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            Vector2 startComponent = getComponent(startKeyframe.Value),
                middleComponent = getComponent(middleKeyframe.Value), endComponent = getComponent(endKeyframe.Value);
            Vector3 start = new(startKeyframe.Time, startComponent.X, startComponent.Y),
                middle = new(middleKeyframe.Time, middleComponent.X, middleComponent.Y),
                end = new(endKeyframe.Time, endComponent.X, endComponent.Y);

            Vector3 startToMiddle = middle - start, startToEnd = end - start;
            return (startToMiddle - Vector3.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd)
                .LengthSquared();
        });

    /// <summary> Simplifies keyframes with 3-parameter values. </summary>
    /// <param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
    /// <param name="getComponent"> Converts the keyframe values to a canonical <see cref="Vector3" /> that the method can use. </param>
    public void Simplify3dKeyframes(float tolerance, Func<TValue, Vector3> getComponent)
        => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            Vector3 startComponent = getComponent(startKeyframe.Value),
                middleComponent = getComponent(middleKeyframe.Value), endComponent = getComponent(endKeyframe.Value);
            Vector4 start = new(startKeyframe.Time, startComponent.X, startComponent.Y, startComponent.Z),
                middle = new(middleKeyframe.Time, middleComponent.X, middleComponent.Y, middleComponent.Z),
                end = new(endKeyframe.Time, endComponent.X, endComponent.Y, endComponent.Z);

            Vector4 startToMiddle = middle - start, startToEnd = end - start;
            return (startToMiddle - Vector4.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd)
                .LengthSquared();
        });

    /// <summary> Smooths a list of keyframes. </summary>
    /// <param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
    /// <param name="getDistanceSq"> A function that gets the squared distance between three specific keyframes. </param>
    public void SimplifyKeyframes(float tolerance,
        Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistanceSq)
    {
        if (tolerance <= .00001f)
        {
            List<Keyframe<TValue>> unionKeyframes = [];
            for (int i = 0, count = keyframes.Count; i < count; i++)
            {
                var startKeyframe = keyframes[i];
                unionKeyframes.Add(startKeyframe);

                for (var j = i + 1; j < count; j++)
                {
                    var endKeyframe = keyframes[j];
                    if (!startKeyframe.Value.Equals(endKeyframe.Value))
                    {
                        if (i < j - 1) unionKeyframes.Add(keyframes[j - 1]);
                        unionKeyframes.Add(endKeyframe);
                        i = j;
                        break;
                    }

                    if (j == count - 1) i = j;
                }
            }

            Clear(true);
            keyframes = unionKeyframes;
            return;
        }

        if (keyframes.Count < 3) return;

        var lastPoint = keyframes.Count - 1;
        List<int> keep = [0, lastPoint];
        getSimplifiedKeyframeIndexes(ref keep, 0, lastPoint, tolerance * tolerance, getDistanceSq);
        if (keep.Count == keyframes.Count) return;

        List<Keyframe<TValue>> simplifiedKeyframes = new(keep.Count);
        keep.Sort();
        simplifiedKeyframes.AddRange(keep.Select(t => keyframes[t]));

        Clear(true);
        keyframes = simplifiedKeyframes;
    }

    void getSimplifiedKeyframeIndexes(ref List<int> keep, int first, int last, float epsilonSq,
        Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistance)
    {
        while (true)
        {
            var start = keyframes[first];
            var end = keyframes[last];

            var maxDistSq = 0f;
            var indexFar = 0;

            for (var i = first; i < last; ++i)
            {
                var distanceSq = getDistance(start, keyframes[i], end);
                if (distanceSq < maxDistSq) continue;
                maxDistSq = distanceSq;
                indexFar = i;
            }

            if (maxDistSq < epsilonSq || indexFar <= 0) return;
            getSimplifiedKeyframeIndexes(ref keep, first, indexFar, epsilonSq, getDistance);
            keep.Add(indexFar);
            first = indexFar;
        }
    }

#endregion
}