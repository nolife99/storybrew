﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace StorybrewCommon.Animations
{
    ///<summary> A mechanism for handling groups of keyframes. </summary>
    ///<typeparam name="TValue"> The type value of the keyframed value. </typeparam>
    public class KeyframedValue<TValue> : MarshalByRefObject, IEnumerable<Keyframe<TValue>>
    {
        List<Keyframe<TValue>> keyframes = new();
        readonly Func<TValue, TValue, double, TValue> interpolate;
        readonly TValue defaultValue;

        ///<summary> Returns the start time of the first keyframe in the keyframed value. </summary>
        public double StartTime => keyframes.Count == 0 ? int.MinValue : keyframes[0].Time;

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

        ///<summary> Constructs a new keyframed value. </summary>
        ///<param name="interpolate"> The <see cref="InterpolatingFunctions"/> to use to interpolate between values. Required to use <see cref="ValueAt(double)"/> </param>
        ///<param name="defaultValue"> The default value of the type of this keyframed value. </param>
        public KeyframedValue(Func<TValue, TValue, double, TValue> interpolate = null, TValue defaultValue = default)
        {
            this.interpolate = interpolate;
            this.defaultValue = defaultValue;
        }

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
        public KeyframedValue<TValue> Add(double time, TValue value, bool before = false) => Add(time, value, EasingFunctions.Linear, before);

        ///<summary> Adds a manually constructed keyframe to the keyframed value. </summary>
        ///<param name="time"> The time of the <see cref="Keyframe{TValue}"/>. </param>
        ///<param name="value"> The type value of the <see cref="Keyframe{TValue}"/>. </param>
        ///<param name="easing"> The <see cref="EasingFunctions"/> type of this <see cref="Keyframe{TValue}"/>. </param>
        ///<param name="before"> If a <see cref="Keyframe{TValue}"/> exists at this time, places new one before existing one. </param>
        public KeyframedValue<TValue> Add(double time, TValue value, Func<double, double> easing, bool before = false) => Add(new Keyframe<TValue>(time, value, easing), before);

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
            to.AddRange(this);
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
        ///<param name="pair"> A delegate encapsulating the start and end keyframe of a pair. </param>
        ///<param name="defaultValue"> The default value if there are no keyframes. </param>
        ///<param name="edit"> A delegate encapsulating edits to the pairs' values. </param>
        ///<param name="explicitStartTime"> The explicit start time for the keyframed value in this method. </param>
        ///<param name="explicitEndTime"> The explicit end time for the keyframed value in this method. </param>
        ///<param name="loopable"> Whether <paramref name="pair"/> is encapsulated in a <see cref="Storyboarding.Commands.LoopCommand"/>. </param>
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
            if (!hasPair && keyframes.Count > 0)
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

        static Keyframe<TValue> editKeyframe(Keyframe<TValue> keyframe, Func<TValue, TValue> edit = null) => edit != null ?
            new Keyframe<TValue>(keyframe.Time, edit(keyframe.Value), keyframe.Ease, keyframe.Until) : keyframe;

        ///<summary> Removes all keyframes in the keyframed value. </summary>
        public void Clear(bool trim = false)
        {
            keyframes.Clear();
            if (trim) keyframes.Capacity = 0;
        }

        ///<summary> Returns an enumerator that iterates through the keyframed value. </summary>
        public IEnumerator<Keyframe<TValue>> GetEnumerator() => keyframes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => keyframes.GetEnumerator();

        int indexFor(Keyframe<TValue> keyframe, bool before)
        {
            var i = keyframes.BinarySearch(keyframe, keyframe);
            if (i >= 0)
            {
                if (before) while (i > 0 && keyframes[i].Time >= keyframe.Time) --i;
                else while (i < keyframes.Count && keyframes[i].Time <= keyframe.Time) ++i;
            }
            else i = ~i;
            return i;
        }
        internal int indexAt(double time, bool before) => indexFor(new Keyframe<TValue>(time), before);

        #region Manipulation

        ///<summary> Simplifies keyframes on 1-parameter commands. </summary>
        ///<param name="tolerance"> Distance threshold from which keyframes can be removed.  </param>
        ///<param name="getComponent"> Converts the keyframe values to a <see cref="float"/> that the method can use. </param>
        public void Simplify1dKeyframes(double tolerance, Func<TValue, float> getComponent)
            => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            var start = new Vector2((float)startKeyframe.Time, getComponent(startKeyframe.Value));
            var middle = new Vector2((float)middleKeyframe.Time, getComponent(middleKeyframe.Value));
            var end = new Vector2((float)endKeyframe.Time, getComponent(endKeyframe.Value));

            var area = Math.Abs((start.X * end.Y + end.X * middle.Y + middle.X * start.Y - end.X * start.Y - middle.X * end.Y - start.X * middle.Y) * .5);
            var bottom = Math.Sqrt((start.X - end.X) * (start.X - end.X) + (start.Y - end.Y) * (start.Y - end.Y));
            return (float)(area / bottom * 2);
        });

        ///<summary> Simplifies keyframes on 2-parameter commands. </summary>
        ///<param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
        ///<param name="getComponent"> Converts the keyframe values to a <see cref="Vector2"/> that the method can use. </param>
        public void Simplify2dKeyframes(double tolerance, Func<TValue, Vector2> getComponent)
            => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            var startComponent = getComponent(startKeyframe.Value);
            var middleComponent = getComponent(middleKeyframe.Value);
            var endComponent = getComponent(endKeyframe.Value);

            var start = new Vector3((float)startKeyframe.Time, startComponent.X, startComponent.Y);
            var middle = new Vector3((float)middleKeyframe.Time, middleComponent.X, middleComponent.Y);
            var end = new Vector3((float)endKeyframe.Time, endComponent.X, endComponent.Y);

            var startToMiddle = middle - start;
            var startToEnd = end - start;
            return (startToMiddle - Vector3.Dot(startToMiddle, startToEnd) / Vector3.Dot(startToEnd, startToEnd) * startToEnd).Length();
        });

        ///<summary> Simplifies keyframes on 3-parameter commands. </summary>
        ///<param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
        ///<param name="getComponent"> Converts the keyframe values to a <see cref="Vector3"/> that the method can use. </param>
        public void Simplify3dKeyframes(double tolerance, Func<TValue, Vector3> getComponent)
            => SimplifyKeyframes(tolerance, (startKeyframe, middleKeyframe, endKeyframe) =>
        {
            var startComponent = getComponent(startKeyframe.Value);
            var middleComponent = getComponent(middleKeyframe.Value);
            var endComponent = getComponent(endKeyframe.Value);

            var start = new Vector4((float)startKeyframe.Time, startComponent.X, startComponent.Y, startComponent.Z);
            var middle = new Vector4((float)middleKeyframe.Time, middleComponent.X, middleComponent.Y, middleComponent.Z);
            var end = new Vector4((float)endKeyframe.Time, endComponent.X, endComponent.Y, endComponent.Z);

            var startToMiddle = middle - start;
            var startToEnd = end - start;
            return (startToMiddle - Vector4.Dot(startToMiddle, startToEnd) / Vector4.Dot(startToEnd, startToEnd) * startToEnd).Length();
        });

        void SimplifyEqualKeyframes()
        {
            var simplifiedKeyframes = new List<Keyframe<TValue>>();
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

            simplifiedKeyframes.TrimExcess();
            keyframes = simplifiedKeyframes;
        }

        ///<summary> Simplifies keyframes on commands. </summary>
        ///<param name="tolerance"> Distance threshold from which keyframes can be removed. </param>
        ///<param name="getDistance"> A function that gets the distance between three specific keyframes. </param>
        public void SimplifyKeyframes(double tolerance, Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistance)
        {
            if (tolerance <= 0)
            {
                SimplifyEqualKeyframes();
                return;
            }
            if (keyframes.Count < 3) return;

            var firstPoint = 0;
            var lastPoint = keyframes.Count - 1;
            var keyframesToKeep = new SortedSet<int> { firstPoint, lastPoint };
            getSimplifiedKeyframeIndexes(ref keyframesToKeep, firstPoint, lastPoint, tolerance, getDistance);

            if (keyframesToKeep.Count == keyframes.Count) return;

            var simplifiedKeyframes = new List<Keyframe<TValue>>(keyframesToKeep.Count);
            foreach (var keep in keyframesToKeep)
            {
                var keyframe = keyframes[keep];
                simplifiedKeyframes.Add(new Keyframe<TValue>(keyframe.Time, keyframe.Value));
            }
            keyframes = simplifiedKeyframes;
        }
        void getSimplifiedKeyframeIndexes(ref SortedSet<int> keyframesToKeep, int firstPoint, int lastPoint, double tolerance, Func<Keyframe<TValue>, Keyframe<TValue>, Keyframe<TValue>, float> getDistance)
        {
            var start = keyframes[firstPoint];
            var end = keyframes[lastPoint];

            var maxDistance = 0d;
            var indexFarthest = 0;
            for (var i = firstPoint; i < lastPoint; ++i)
            {
                var middle = keyframes[i];
                var distance = getDistance(start, middle, end);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = i;
                }
            }
            if (maxDistance > tolerance && indexFarthest > 0)
            {
                keyframesToKeep.Add(indexFarthest);
                getSimplifiedKeyframeIndexes(ref keyframesToKeep, firstPoint, indexFarthest, tolerance, getDistance);
                getSimplifiedKeyframeIndexes(ref keyframesToKeep, indexFarthest, lastPoint, tolerance, getDistance);
            }
        }

        #endregion
    }
}