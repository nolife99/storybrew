using StorybrewCommon.Storyboarding;
using System;

namespace StorybrewCommon.Animations
{
    ///<summary> A static class providing keyframing easing functions. </summary>
    public static class EasingFunctions
    {
        ///<summary> Reverses an easing function. </summary>
        public static double Reverse(Func<double, double> func, double value) => 1 - func(1 - value);

        ///<summary> Converts an easing function to its in-out counterpart. </summary>
        public static double ToInOut(Func<double, double> func, double value) => (value < .5 ? func(2 * value) : (2 - func(2 - 2 * value))) / 2;

        ///<summary> An easing function that represents the integer value of the progression. </summary>
        public static Func<double, double> Step = x => x >= 1 ? 1 : 0;
        ///<summary> An easing function that represents a linear progression. </summary>
        public static Func<double, double> Linear = x => x;

        ///<summary> An easing function that represents a basic easing-in progression. </summary>
        public static Func<double, double> In = x => Math.Pow(x, 1.5);
        ///<summary> An easing function that represents a basic easing-out progression. </summary>
        public static Func<double, double> Out = x => Reverse(In, x);
        ///<summary> An easing function that represents a basic easing-in-out progression. </summary>
        public static Func<double, double> InOut = x => ToInOut(In, x);

        ///<summary> An easing function that represents a squared easing-in progression. </summary>
        public static Func<double, double> QuadIn = x => x * x;
        ///<summary> An easing function that represents a squared easing-out progression. </summary>
        public static Func<double, double> QuadOut = x => Reverse(QuadIn, x);
        ///<summary> An easing function that represents a squared easing-in-out progression. </summary>
        public static Func<double, double> QuadInOut = x => ToInOut(QuadIn, x);

        ///<summary> An easing function that represents a cubed easing-in progression. </summary>
        public static Func<double, double> CubicIn = x => x * x * x;
        ///<summary> An easing function that represents a cubed easing-out progression. </summary>
        public static Func<double, double> CubicOut = x => Reverse(CubicIn, x);
        ///<summary> An easing function that represents a cubed easing-in-out progression. </summary>
        public static Func<double, double> CubicInOut = x => ToInOut(CubicIn, x);

        ///<summary> An easing function that represents a quartic easing-in progression. </summary>
        public static Func<double, double> QuartIn = x => x * x * x * x;
        ///<summary> An easing function that represents a quartic easing-out progression. </summary>
        public static Func<double, double> QuartOut = x => Reverse(QuartIn, x);
        ///<summary> An easing function that represents a quartic easing-in-out progression. </summary>
        public static Func<double, double> QuartInOut = x => ToInOut(QuartIn, x);

        ///<summary> An easing function that represents a quintic easing-in progression. </summary>
        public static Func<double, double> QuintIn = x => x * x * x * x * x;
        ///<summary> An easing function that represents a quintic easing-out progression. </summary>
        public static Func<double, double> QuintOut = x => Reverse(QuintIn, x);
        ///<summary> An easing function that represents a quintic easing-in-out progression. </summary>
        public static Func<double, double> QuintInOut = x => ToInOut(QuintIn, x);

        ///<summary> An easing function that represents a sinusoidal easing-in progression. </summary>
        public static Func<double, double> SineIn = x => 1 - Math.Cos(x * Math.PI / 2);
        ///<summary> An easing function that represents a sinusoidal easing-out progression. </summary>
        public static Func<double, double> SineOut = x => Reverse(SineIn, x);
        ///<summary> An easing function that represents a sinusoidal easing-in-out progression. </summary>
        public static Func<double, double> SineInOut = x => ToInOut(SineIn, x);

        ///<summary> An easing function that represents a exponential easing-in progression. </summary>
        public static Func<double, double> ExpoIn = x => Math.Pow(2, 10 * (x - 1));
        ///<summary> An easing function that represents a exponential easing-out progression. </summary>
        public static Func<double, double> ExpoOut = x => Reverse(ExpoIn, x);
        ///<summary> An easing function that represents a exponential easing-in-out progression. </summary>
        public static Func<double, double> ExpoInOut = x => ToInOut(ExpoIn, x);

        ///<summary> An easing function that represents a circular easing-in progression. </summary>
        public static Func<double, double> CircIn = x => 1 - Math.Sqrt(1 - x * x);
        ///<summary> An easing function that represents a circular easing-out progression. </summary>
        public static Func<double, double> CircOut = x => Reverse(CircIn, x);
        ///<summary> An easing function that represents a circular easing-in-out progression. </summary>
        public static Func<double, double> CircInOut = x => ToInOut(CircIn, x);

        ///<summary> An easing function that represents a overshooting easing-in progression. </summary>
        public static Func<double, double> BackIn = x => x * x * ((1.70158 + 1) * x - 1.70158);
        ///<summary> An easing function that represents a overshooting easing-out progression. </summary>
        public static Func<double, double> BackOut = x => Reverse(BackIn, x);
        ///<summary> An easing function that represents a overshooting easing-in-out progression. </summary>
        public static Func<double, double> BackInOut = x => ToInOut(y => y * y * ((1.70158 * 1.525 + 1) * y - 1.70158 * 1.525), x);

        ///<summary> An easing function that represents a bouncing easing-in progression. </summary>
        public static Func<double, double> BounceIn = x => Reverse(BounceOut, x);
        ///<summary> An easing function that represents a bouncing easing-out progression. </summary>
        public static Func<double, double> BounceOut = x => x < 1 / 2.75 ?
            7.5625 * x * x : x < 2 / 2.75 ? 
            7.5625 * (x -= 1.5 / 2.75) * x + .75 : x < 2.5 / 2.75 ? 
            7.5625 * (x -= 2.25 / 2.75) * x + .9375 : 7.5625 * (x -= 2.625 / 2.75) * x + .984375;
        ///<summary> An easing function that represents a bouncing easing-in-out progression. </summary>
        public static Func<double, double> BounceInOut = x => ToInOut(BounceIn, x);

        ///<summary> An easing function that represents a elastic (springy) easing-in progression. </summary>
        public static Func<double, double> ElasticIn = x => Reverse(ElasticOut, x);
        ///<summary> An easing function that represents a elastic (springy) easing-out progression. </summary>
        public static Func<double, double> ElasticOut = x => Math.Pow(2, -10 * x) * Math.Sin((x - .075) * (2 * Math.PI) / .3) + 1;
        ///<summary> An easing function that represents a elastic half-intensity easing-out progression. </summary>
        public static Func<double, double> ElasticOutHalf = x => Math.Pow(2, -10 * x) * Math.Sin((.5 * x - .075) * (2 * Math.PI) / .3) + 1;
        ///<summary> An easing function that represents a elastic quarter-intensity easing-out progression. </summary>
        public static Func<double, double> ElasticOutQuarter = x => Math.Pow(2, -10 * x) * Math.Sin((.25 * x - .075) * (2 * Math.PI) / .3) + 1;
        ///<summary> An easing function that represents a elastic easing-in-out progression. </summary>
        public static Func<double, double> ElasticInOut = x => ToInOut(ElasticIn, x);

        ///<summary> Applies the specified <see cref="OsbEasing"/> to the progress (<paramref name="value"/>). </summary>
        public static double Ease(this OsbEasing easing, double value) => ToEasingFunction(easing).Invoke(value);

        ///<summary> Converts an <see cref="OsbEasing"/> to one of the corresponding <see cref="EasingFunctions"/>. </summary>
        public static Func<double, double> ToEasingFunction(OsbEasing easing)
        {
            switch (easing)
            {
                default:
                case OsbEasing.None: return Linear;

                case OsbEasing.In: return In;
                case OsbEasing.InQuad: return QuadIn;
                case OsbEasing.Out: return Out;
                case OsbEasing.OutQuad: return QuadOut;
                case OsbEasing.InOutQuad: return QuadInOut;

                case OsbEasing.InCubic: return CubicIn;
                case OsbEasing.OutCubic: return CubicOut;
                case OsbEasing.InOutCubic: return CubicInOut;

                case OsbEasing.InQuart: return QuartIn;
                case OsbEasing.OutQuart: return QuartOut;
                case OsbEasing.InOutQuart: return QuartInOut;

                case OsbEasing.InQuint: return QuintIn;
                case OsbEasing.OutQuint: return QuintOut;
                case OsbEasing.InOutQuint: return QuintInOut;

                case OsbEasing.InSine: return SineIn;
                case OsbEasing.OutSine: return SineOut;
                case OsbEasing.InOutSine: return SineInOut;

                case OsbEasing.InExpo: return ExpoIn;
                case OsbEasing.OutExpo: return ExpoOut;
                case OsbEasing.InOutExpo: return ExpoInOut;

                case OsbEasing.InCirc: return CircIn;
                case OsbEasing.OutCirc: return CircOut;
                case OsbEasing.InOutCirc: return CircInOut;

                case OsbEasing.InElastic: return ElasticIn;
                case OsbEasing.OutElastic: return ElasticOut;
                case OsbEasing.OutElasticHalf: return ElasticOutHalf;
                case OsbEasing.OutElasticQuarter: return ElasticOutQuarter;
                case OsbEasing.InOutElastic: return ElasticInOut;

                case OsbEasing.InBack: return BackIn;
                case OsbEasing.OutBack: return BackOut;
                case OsbEasing.InOutBack: return BackInOut;

                case OsbEasing.InBounce: return BounceIn;
                case OsbEasing.OutBounce: return BounceOut;
                case OsbEasing.InOutBounce: return BounceInOut;
            }
        }
    }
}