using System;
using StorybrewCommon.Storyboarding;

namespace StorybrewCommon.Animations;

///<summary> A static class providing keyframing easing functions. </summary>
public static class EasingFunctions
{
    ///<summary> Reverses an easing function. </summary>
    public static double Reverse(Func<double, double> func, double value) => 1 - func(1 - value);

    ///<summary> Converts an easing function to its in-out counterpart. </summary>
    public static double ToInOut(Func<double, double> func, double value) => (value < .5 ? func(2 * value) : (2 - func(2 - 2 * value))) * .5;

    ///<summary> An easing function that represents the integer value of the progression. </summary>
    public static readonly Func<double, double> Step = x => x >= 1 ? 1 : 0;
    ///<summary> An easing function that represents a linear progression. </summary>
    public static readonly Func<double, double> Linear = x => x;

    ///<summary> An easing function that represents a basic easing-in progression. </summary>
    public static readonly Func<double, double> In = x => Math.Pow(x, 1.5);
    ///<summary> An easing function that represents a basic easing-out progression. </summary>
    public static readonly Func<double, double> Out = x => Reverse(In, x);
    ///<summary> An easing function that represents a basic easing-in-out progression. </summary>
    public static readonly Func<double, double> InOut = x => ToInOut(In, x);

    ///<summary> An easing function that represents a squared easing-in progression. </summary>
    public static readonly Func<double, double> QuadIn = x => x * x;
    ///<summary> An easing function that represents a squared easing-out progression. </summary>
    public static readonly Func<double, double> QuadOut = x => Reverse(QuadIn, x);
    ///<summary> An easing function that represents a squared easing-in-out progression. </summary>
    public static readonly Func<double, double> QuadInOut = x => ToInOut(QuadIn, x);

    ///<summary> An easing function that represents a cubed easing-in progression. </summary>
    public static readonly Func<double, double> CubicIn = x => x * x * x;
    ///<summary> An easing function that represents a cubed easing-out progression. </summary>
    public static readonly Func<double, double> CubicOut = x => Reverse(CubicIn, x);
    ///<summary> An easing function that represents a cubed easing-in-out progression. </summary>
    public static readonly Func<double, double> CubicInOut = x => ToInOut(CubicIn, x);

    ///<summary> An easing function that represents a quartic easing-in progression. </summary>
    public static readonly Func<double, double> QuartIn = x => x * x * x * x;
    ///<summary> An easing function that represents a quartic easing-out progression. </summary>
    public static readonly Func<double, double> QuartOut = x => Reverse(QuartIn, x);
    ///<summary> An easing function that represents a quartic easing-in-out progression. </summary>
    public static readonly Func<double, double> QuartInOut = x => ToInOut(QuartIn, x);

    ///<summary> An easing function that represents a quintic easing-in progression. </summary>
    public static readonly Func<double, double> QuintIn = x => x * x * x * x * x;
    ///<summary> An easing function that represents a quintic easing-out progression. </summary>
    public static readonly Func<double, double> QuintOut = x => Reverse(QuintIn, x);
    ///<summary> An easing function that represents a quintic easing-in-out progression. </summary>
    public static readonly Func<double, double> QuintInOut = x => ToInOut(QuintIn, x);

    ///<summary> An easing function that represents a sinusoidal easing-in progression. </summary>
    public static readonly Func<double, double> SineIn = x => 1 - Math.Cos(x * Math.PI * .5);
    ///<summary> An easing function that represents a sinusoidal easing-out progression. </summary>
    public static readonly Func<double, double> SineOut = x => Reverse(SineIn, x);
    ///<summary> An easing function that represents a sinusoidal easing-in-out progression. </summary>
    public static readonly Func<double, double> SineInOut = x => ToInOut(SineIn, x);

    ///<summary> An easing function that represents a exponential easing-in progression. </summary>
    public static readonly Func<double, double> ExpoIn = x => Math.Pow(2, 10 * (x - 1));
    ///<summary> An easing function that represents a exponential easing-out progression. </summary>
    public static readonly Func<double, double> ExpoOut = x => Reverse(ExpoIn, x);
    ///<summary> An easing function that represents a exponential easing-in-out progression. </summary>
    public static readonly Func<double, double> ExpoInOut = x => ToInOut(ExpoIn, x);

    ///<summary> An easing function that represents a circular easing-in progression. </summary>
    public static readonly Func<double, double> CircIn = x => 1 - Math.Sqrt(1 - x * x);
    ///<summary> An easing function that represents a circular easing-out progression. </summary>
    public static readonly Func<double, double> CircOut = x => Reverse(CircIn, x);
    ///<summary> An easing function that represents a circular easing-in-out progression. </summary>
    public static readonly Func<double, double> CircInOut = x => ToInOut(CircIn, x);

    ///<summary> An easing function that represents a overshooting easing-in progression. </summary>
    public static readonly Func<double, double> BackIn = x => x * x * ((1.70158 + 1) * x - 1.70158);
    ///<summary> An easing function that represents a overshooting easing-out progression. </summary>
    public static readonly Func<double, double> BackOut = x => Reverse(BackIn, x);
    ///<summary> An easing function that represents a overshooting easing-in-out progression. </summary>
    public static readonly Func<double, double> BackInOut = x => ToInOut(y => y * y * ((1.70158 * 1.525 + 1) * y - 1.70158 * 1.525), x);

    ///<summary> An easing function that represents a bouncing easing-in progression. </summary>
    public static readonly Func<double, double> BounceIn = x => Reverse(BounceOut, x);
    ///<summary> An easing function that represents a bouncing easing-out progression. </summary>
    public static readonly Func<double, double> BounceOut = x => x < 1 / 2.75 ?
        7.5625 * x * x : x < 2 / 2.75 ? 
        7.5625 * (x -= 1.5 / 2.75) * x + .75 : x < 2.5 / 2.75 ? 
        7.5625 * (x -= 2.25 / 2.75) * x + .9375 : 7.5625 * (x -= 2.625 / 2.75) * x + .984375;
    ///<summary> An easing function that represents a bouncing easing-in-out progression. </summary>
    public static readonly Func<double, double> BounceInOut = x => ToInOut(BounceIn, x);

    ///<summary> An easing function that represents a elastic (springy) easing-in progression. </summary>
    public static readonly Func<double, double> ElasticIn = x => Reverse(ElasticOut, x);
    ///<summary> An easing function that represents a elastic (springy) easing-out progression. </summary>
    public static readonly Func<double, double> ElasticOut = x => Math.Pow(2, -10 * x) * Math.Sin((x - .075) * (2 * Math.PI) / .3) + 1;
    ///<summary> An easing function that represents a elastic half-intensity easing-out progression. </summary>
    public static readonly Func<double, double> ElasticOutHalf = x => Math.Pow(2, -10 * x) * Math.Sin((.5 * x - .075) * (2 * Math.PI) / .3) + 1;
    ///<summary> An easing function that represents a elastic quarter-intensity easing-out progression. </summary>
    public static readonly Func<double, double> ElasticOutQuarter = x => Math.Pow(2, -10 * x) * Math.Sin((.25 * x - .075) * (2 * Math.PI) / .3) + 1;
    ///<summary> An easing function that represents a elastic easing-in-out progression. </summary>
    public static readonly Func<double, double> ElasticInOut = x => ToInOut(ElasticIn, x);

    ///<summary> Applies the specified <see cref="OsbEasing"/> to the progress (<paramref name="value"/>). </summary>
    public static double Ease(this OsbEasing easing, double value) => ToEasingFunction(easing)(value);

    ///<summary> Converts an <see cref="OsbEasing"/> to one of the corresponding <see cref="EasingFunctions"/>. </summary>
    public static Func<double, double> ToEasingFunction(OsbEasing easing) => easing switch
    {
        OsbEasing.In => In,
        OsbEasing.InQuad => QuadIn,
        OsbEasing.Out => Out,
        OsbEasing.OutQuad => QuadOut,
        OsbEasing.InOutQuad => QuadInOut,
        OsbEasing.InCubic => CubicIn,
        OsbEasing.OutCubic => CubicOut,
        OsbEasing.InOutCubic => CubicInOut,
        OsbEasing.InQuart => QuartIn,
        OsbEasing.OutQuart => QuartOut,
        OsbEasing.InOutQuart => QuartInOut,
        OsbEasing.InQuint => QuintIn,
        OsbEasing.OutQuint => QuintOut,
        OsbEasing.InOutQuint => QuintInOut,
        OsbEasing.InSine => SineIn,
        OsbEasing.OutSine => SineOut,
        OsbEasing.InOutSine => SineInOut,
        OsbEasing.InExpo => ExpoIn,
        OsbEasing.OutExpo => ExpoOut,
        OsbEasing.InOutExpo => ExpoInOut,
        OsbEasing.InCirc => CircIn,
        OsbEasing.OutCirc => CircOut,
        OsbEasing.InOutCirc => CircInOut,
        OsbEasing.InElastic => ElasticIn,
        OsbEasing.OutElastic => ElasticOut,
        OsbEasing.OutElasticHalf => ElasticOutHalf,
        OsbEasing.OutElasticQuarter => ElasticOutQuarter,
        OsbEasing.InOutElastic => ElasticInOut,
        OsbEasing.InBack => BackIn,
        OsbEasing.OutBack => BackOut,
        OsbEasing.InOutBack => BackInOut,
        OsbEasing.InBounce => BounceIn,
        OsbEasing.OutBounce => BounceOut,
        OsbEasing.InOutBounce => BounceInOut,
        _ => Linear,
    };
}