namespace StorybrewCommon.Animations;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Memory;

/// <summary>
///     A set of keyframes, each with a time and value of type <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TValue"> The type of values of the keyframes. </typeparam>
/// <remarks>
///     See <see cref="Keyframe{TValue}"/> for more information about keyframes.
/// </remarks>
public class KeyframedValue<TValue>(Func<TValue, TValue, float, TValue> interpolate = null, TValue defaultValue = default)
    : IEnumerable<Keyframe<TValue>>
{
    internal List<Keyframe<TValue>> keyframes = [];

    ///<summary> Returns the time of the first keyframe. </summary>
    public float StartTime => keyframes.Count == 0 ? int.MaxValue : keyframes[0].Time;

    ///<summary> Returns the time of the last keyframe. </summary>
    public float EndTime => keyframes.Count == 0 ? int.MinValue : keyframes[^1].Time;

    ///<summary> Gets the value of the first keyframe. </summary>
    public TValue StartValue => keyframes.Count == 0 ? defaultValue : keyframes[0].Value;

    ///<summary> Gets the value of the last keyframe. </summary>
    public TValue EndValue => keyframes.Count == 0 ? defaultValue : keyframes[^1].Value;

    /// <summary>
    ///     Gets or sets the keyframe at the specified index.
    /// </summary>
    /// <value>
    ///     The keyframe at the specified index.
    /// </value>
    /// <param name="index"> The index of the keyframe. </param>
    /// <returns> The keyframe at the specified index. </returns>
    public Keyframe<TValue> this[int index] { get => keyframes[index]; set => keyframes[index] = value; }

    ///<summary> The amount of keyframes in the set. </summary>
    public int Count => keyframes.Count;

    IEnumerator<Keyframe<TValue>> IEnumerable<Keyframe<TValue>>.GetEnumerator() => keyframes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => keyframes.GetEnumerator();

    /// <summary>
    ///     Adds a keyframe to the keyframed value.
    /// </summary>
    /// <param name="keyframe"> The keyframe to add. </param>
    /// <param name="before"> Whether to add the keyframe before any keyframes at the same time. </param>
    /// <returns> The keyframed value. </returns>
    public KeyframedValue<TValue> Add(Keyframe<TValue> keyframe, bool before = false)
    {
        if (keyframes.Count == 0 || keyframes[^1].Time < keyframe.Time) keyframes.Add(keyframe);
        else keyframes.Insert(indexFor(keyframe, before), keyframe);

        return this;
    }

    /// <summary>
    ///     Adds a set of keyframes to the keyframed value.
    /// </summary>
    /// <param name="values"> The set of keyframes to add. </param>
    /// <returns> The keyframed value. </returns>
    public KeyframedValue<TValue> Add(params Keyframe<TValue>[] values) => AddRange(values);

    /// <summary>
    ///     Adds a new keyframe to the keyframed value at the given time with the given value.
    /// </summary>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="value"> The value of the new keyframe. </param>
    /// <param name="before"> Whether to add the new keyframe before any keyframes at the same time. </param>
    /// <returns> The keyframed value. </returns>
    public KeyframedValue<TValue> Add(float time, TValue value, bool before = false) => Add(new(time, value), before);

    /// <summary>
    ///     Adds a new keyframe to the keyframed value at the given time with the given value and easing.
    /// </summary>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <param name="value"> The value of the new keyframe. </param>
    /// <param name="easing"> The easing to apply to the new keyframe. </param>
    /// <param name="before"> Whether to add the new keyframe before any keyframes at the same time. </param>
    /// <returns> The keyframed value. </returns>
    public KeyframedValue<TValue> Add(float time, TValue value, Func<float, float> easing, bool before = false)
        => Add(new(time, value, easing), before);

    /// <summary>
    ///     Adds a new keyframe to the keyframed value at the given time with the value at that time.
    /// </summary>
    /// <param name="time"> The time of the new keyframe. </param>
    /// <returns> The keyframed value. </returns>
    public KeyframedValue<TValue> Add(float time) => Add(time, ValueAt(time));

    /// <summary>
    ///     Adds a set of keyframes to the keyframed value.
    /// </summary>
    /// <param name="collection"> The set of keyframes to add. </param>
    /// <returns> The keyframed value. </returns>
    public KeyframedValue<TValue> AddRange(IEnumerable<Keyframe<TValue>> collection)
    {
        if (collection is ICollection<Keyframe<TValue>> list) keyframes.EnsureCapacity(keyframes.Count + list.Count);
        foreach (var keyframe in collection)
            if (keyframes.Count == 0 || keyframes[^1].Time < keyframe.Time) keyframes.Add(keyframe);
            else keyframes.Insert(indexFor(keyframe, false), keyframe);

        return this;
    }

    ///<summary> Waits from at the end of the previous keyframe until the given time. </summary>
    public KeyframedValue<TValue> Until(float time) => keyframes.Count == 0 ? this : Add(time, EndValue);

    internal KeyframedValue<TValue> DebugUntil(float time) => Add(new Keyframe<TValue>(time, EndValue, null, true));

    /// <summary>
    ///     Transfers all keyframes from this keyframe collection to <paramref name="to"/>.
    /// </summary>
    /// <param name="to"> The destination keyframe collection. </param>
    /// <param name="clear"> If <see langword="true"/>, clears the keyframes from this keyframe collection. </param>
    public void TransferKeyframes(KeyframedValue<TValue> to, bool clear = true)
    {
        if (Count == 0) return;
        to.AddRange(keyframes);
        if (clear) Clear();
    }

    /// <summary>
    ///     Gets the value of the keyframed value at the specified time by interpolating between keyframes.
    /// </summary>
    /// <param name="time">The time at which to get the value.</param>
    /// <returns>The interpolated value at the specified time.</returns>
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

        if (interpolate is null)
            throw new InvalidOperationException("Cannot interpolate keyframes without an interpolation function");

        var from = keyframes[i - 1];
        var to = keyframes[i];

        return from.Time == to.Time ?
            to.Value :
            interpolate(from.Value, to.Value, to.Ease((time - from.Time) / (to.Time - from.Time)));
    }

    /// <summary>
    ///     Enumerates each pair of adjacent keyframes in the keyframed value.
    /// </summary>
    /// <param name="pair"> The action to invoke for each pair of adjacent keyframes. </param>
    /// <param name="defaultValue"> Optional default value to use if the keyframed value is empty. </param>
    /// <param name="edit"> Optional function to edit each keyframe value before passing it to <paramref name="pair"/>. </param>
    /// <param name="explicitStartTime"> Optional time to start enumerating from. If null, the first keyframe time is used. </param>
    /// <param name="explicitEndTime"> Optional time to end enumerating at. If null, the last keyframe time is used. </param>
    /// <param name="loopable"> If true, each keyframe's relative time to the first keyframe is used. </param>
    public void ForEachPair(Action<Keyframe<TValue>, Keyframe<TValue>> pair,
        TValue defaultValue = default,
        Func<TValue, TValue> edit = null,
        float? explicitStartTime = null,
        float? explicitEndTime = null,
        bool loopable = false)
    {
        if (keyframes.Count == 0) return;
        var span = CollectionsMarshal.AsSpan(keyframes);

        var startTime = explicitStartTime ?? span[0].Time;
        var endTime = explicitEndTime ?? span[^1].Time;

        bool hasPair = false, forceNextFlat = loopable;
        Keyframe<TValue>? previous = null, stepStart = null, previousPairEnd = null;
        var comparer = EqualityComparer<TValue>.Default;

        foreach (ref var t in span)
        {
            var endKeyframe = editKeyframe(ref t, edit);
            if (previous.HasValue)
            {
                var startKeyframe = previous.Value;
                var isFlat = comparer.Equals(startKeyframe.Value, endKeyframe.Value);
                var isStep = !isFlat && startKeyframe.Time == endKeyframe.Time;

                if (isStep) stepStart ??= startKeyframe;
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
                pair(initialPair, initialPair);
            }

            pair(stepStart.Value, previous.Value);
            previousPairEnd = previous.Value;
            hasPair = true;
        }

        if (!hasPair && keyframes.Count != 0)
        {
            var first = editKeyframe(ref span[0], edit).WithTime(startTime);
            if (!comparer.Equals(first.Value, defaultValue))
            {
                var last = loopable ? first.WithTime(endTime) : first;
                pair(first, last);
                previousPairEnd = last;
                hasPair = true;
            }
        }

        if (!hasPair || !explicitEndTime.HasValue || previousPairEnd.Value.Time > endTime) return;

        var endPair = previousPairEnd.Value.WithTime(endTime);
        pair(loopable ? previousPairEnd.Value : endPair, endPair);
    }

    static Keyframe<TValue> editKeyframe(ref Keyframe<TValue> keyframe, Func<TValue, TValue> edit = null) => edit is not null ?
        new(keyframe.Time, edit(keyframe.Value), keyframe.Ease, keyframe.Until) :
        keyframe;

    ///<summary> Removes all keyframes in the set. </summary>
    public void Clear(bool trim = false)
    {
        if (keyframes.Count == 0) return;

        keyframes.Clear();
        if (trim) keyframes.Capacity = 0;
    }

    int indexFor(Keyframe<TValue> keyframe, bool before)
    {
        var span = CollectionsMarshal.AsSpan(keyframes);

        var i = span.BinarySearch(keyframe, Keyframe<TValue>.Comparer);
        if (i >= 0)
        {
            if (before)
                while (i > 0 && span[i].Time >= keyframe.Time)
                    --i;
            else
                while (i < span.Length && span[i].Time <= keyframe.Time)
                    ++i;
        }
        else i = ~i;

        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int indexAt(float time, bool before) => indexFor(new(time), before);

    #region Manipulation

    /// <summary>
    ///     Flattens keyframes in the set.
    /// </summary>
    /// <param name="tolerance">
    ///     The tolerance of the keyframe simplification. Values closer to 0 will result in more
    ///     keyframes.
    /// </param>
    /// <param name="getComponent"> A function that extracts a <see cref="float"/> component from the value of a keyframe. </param>
    /// <remarks> This function operates on 1D parameters. </remarks>
    public void Simplify1dKeyframes(float tolerance, Func<TValue, float> getComponent) => SimplifyKeyframes(tolerance,
        (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            Vector2 start = new(startKeyframe.Time, getComponent(startKeyframe.Value)),
                middle = new(middleKeyframe.Time, getComponent(middleKeyframe.Value)),
                end = new(endKeyframe.Time, getComponent(endKeyframe.Value));

            Vector2 startToMiddle = middle - start, startToEnd = end - start;
            return (startToMiddle - Vector2.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd)
                .LengthSquared();
        });

    /// <summary>
    ///     Flattens keyframes in the set.
    /// </summary>
    /// <param name="tolerance">
    ///     The tolerance of the keyframe simplification. Values closer to 0 will result in more
    ///     keyframes.
    /// </param>
    /// <param name="getComponent"> A function that extracts a <see cref="Vector2"/> component from the value of a keyframe. </param>
    /// <remarks> This function operates on 2D parameters. </remarks>
    public void Simplify2dKeyframes(float tolerance, Func<TValue, Vector2> getComponent) => SimplifyKeyframes(tolerance,
        (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            Vector2 startComponent = getComponent(startKeyframe.Value), middleComponent = getComponent(middleKeyframe.Value),
                endComponent = getComponent(endKeyframe.Value);

            Vector3 start = new(startKeyframe.Time, startComponent.X, startComponent.Y),
                middle = new(middleKeyframe.Time, middleComponent.X, middleComponent.Y),
                end = new(endKeyframe.Time, endComponent.X, endComponent.Y);

            Vector3 startToMiddle = middle - start, startToEnd = end - start;
            return (startToMiddle - Vector3.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd)
                .LengthSquared();
        });

    /// <summary>
    ///     Flattens keyframes in the set.
    /// </summary>
    /// <param name="tolerance">
    ///     The tolerance of the keyframe simplification. Values closer to 0 will result in more
    ///     keyframes.
    /// </param>
    /// <param name="getComponent"> A function that extracts a <see cref="Vector3"/> component from the value of a keyframe. </param>
    /// <remarks> This function operates on 3D parameters. </remarks>
    public void Simplify3dKeyframes(float tolerance, Func<TValue, Vector3> getComponent) => SimplifyKeyframes(tolerance,
        (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            Vector3 startComponent = getComponent(startKeyframe.Value), middleComponent = getComponent(middleKeyframe.Value),
                endComponent = getComponent(endKeyframe.Value);

            Vector4 start = new(startKeyframe.Time, startComponent.X, startComponent.Y, startComponent.Z),
                middle = new(middleKeyframe.Time, middleComponent.X, middleComponent.Y, middleComponent.Z),
                end = new(endKeyframe.Time, endComponent.X, endComponent.Y, endComponent.Z);

            Vector4 startToMiddle = middle - start, startToEnd = end - start;
            return (startToMiddle - Vector4.Dot(startToMiddle, startToEnd) / startToEnd.LengthSquared() * startToEnd)
                .LengthSquared();
        });

    void SimplifyKeyframes(float tolerance, Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistanceSq)
    {
        var span = CollectionsMarshal.AsSpan(keyframes);

        if (tolerance <= .00001f)
        {
            List<Keyframe<TValue>> unionKeyframes = [];
            var comparer = EqualityComparer<TValue>.Default;

            for (int i = 0, count = span.Length; i < count; i++)
            {
                var startKeyframe = span[i];
                unionKeyframes.Add(startKeyframe);

                for (var j = i + 1; j < count; j++)
                {
                    var endKeyframe = span[j];
                    if (!comparer.Equals(startKeyframe.Value, endKeyframe.Value))
                    {
                        if (i < j - 1) unionKeyframes.Add(span[j - 1]);
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

        if (span.Length < 3) return;

        var lastPoint = span.Length - 1;
        using UnmanagedList<int> keep = [0, lastPoint];
        getSimplifiedKeyframeIndices(ref span, keep, 0, lastPoint, tolerance * tolerance, getDistanceSq);
        if (keep.Count == span.Length) return;

        List<Keyframe<TValue>> simplifiedKeyframes = new(keep.Count);
        keep.Sort();
        foreach (ref var t in keep) simplifiedKeyframes.Add(span[t]);

        Clear(true);
        keyframes = simplifiedKeyframes;
    }

    static void getSimplifiedKeyframeIndices(ref Span<Keyframe<TValue>> span,
        UnmanagedList<int> keep,
        int first,
        int last,
        float epsilonSq,
        Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistance)
    {
        while (true)
        {
            var start = span[first];
            var end = span[last];

            var maxDistSq = 0f;
            var indexFar = 0;

            for (var i = first; i < last; ++i)
            {
                var distanceSq = getDistance(start, span[i], end);
                if (distanceSq < maxDistSq) continue;
                maxDistSq = distanceSq;
                indexFar = i;
            }

            if (maxDistSq < epsilonSq || indexFar <= 0) return;
            getSimplifiedKeyframeIndices(ref span, keep, first, indexFar, epsilonSq, getDistance);
            keep.Add(ref indexFar);
            first = indexFar;
        }
    }

    #endregion
}