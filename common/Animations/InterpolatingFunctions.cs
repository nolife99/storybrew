using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Numerics;
using BrewLib.Util;

namespace StorybrewCommon.Animations
{
    /// <summary> A static class providing interpolating functions. </summary>
    public static class InterpolatingFunctions
    {
        ///<summary> Represents a function that interpolates between 2 <see cref="float"/>s. </summary>
        public static Func<float, float, double, float> Float = (from, to, progress) => from + (to - from) * (float)progress;

        ///<summary> Represents a function that interpolates between 2 <see cref="float"/> angles. </summary>
        public static Func<float, float, double, float> FloatAngle = (from, to, progress) => from + (float)(MathUtil.ShortestAngleDelta(from, to) * progress);

        ///<summary> Represents a function that interpolates between 2 <see cref="double"/>s. </summary>
        public static Func<double, double, double, double> Double = (from, to, progress) => from + (to - from) * progress;

        ///<summary> Represents a function that interpolates between 2 <see cref="double"/> angles. </summary>
        public static Func<double, double, double, double> DoubleAngle = (from, to, progress) => from + MathUtil.ShortestAngleDelta(from, to) * progress;

        ///<summary> Represents a function that interpolates between 2 <see cref="CommandPosition"/> vectors. </summary>
        public static Func<CommandPosition, CommandPosition, double, CommandPosition> Vector2 = (from, to, progress) => from + (to - from) * (float)progress;

        ///<summary> Represents a function that interpolates between 2 <see cref="CommandScale"/> vectors. </summary>
        public static Func<CommandScale, CommandScale, double, CommandScale> Scale = (from, to, progress) => from + (to - from) * (float)progress;

        ///<summary> Represents a function that interpolates between 2 <see cref="Vector3"/> vectors. </summary>
        public static Func<Vector3, Vector3, double, Vector3> Vector3 = (from, to, progress) => from + (to - from) * (float)progress;

        ///<summary> Represents a function that performs spherical linear interpolation on 2 <see cref="Quaternion"/>s. </summary>
        public static Func<Quaternion, Quaternion, double, Quaternion> QuaternionSlerp = (from, to, progress) => Quaternion.Slerp(from, to, (float)progress);

        ///<summary/>
        public static Func<bool, bool, double, bool> BoolFrom = (from, to, progress) => from;
        ///<summary/>
        public static Func<bool, bool, double, bool> BoolTo = (from, to, progress) => to;
        ///<summary/>
        public static Func<bool, bool, double, bool> BoolAny = (from, to, progress) => from || to;
        ///<summary/>
        public static Func<bool, bool, double, bool> BoolBoth = (from, to, progress) => from && to;

        ///<summary> Represents a function that interpolates between 2 <see cref="Storyboarding.CommandValues.CommandColor"/> RGB values. </summary>
        public static Func<CommandColor, CommandColor, double, CommandColor> CommandColor = (from, to, progress) => from + (to - from) * (float)progress;
    }
}