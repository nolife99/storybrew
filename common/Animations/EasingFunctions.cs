namespace StorybrewCommon.Animations;

using System;
using Storyboarding;

///<summary> A static class providing keyframing easing functions. </summary>
public static class EasingFunctions
{
    ///<summary> An easing function that represents the floor value of the progression. </summary>
    public static double Step(double x) => x >= 1 ? 1 : 0;

    ///<summary> An easing function that represents a linear progression. </summary>
    public static double Linear(double x) => x;

    ///<summary> An easing function that represents an easing-in progression. </summary>
    public static double In(double x) => Math.Pow(x, 1.5f);

    ///<summary> An easing function that represents an easing-out progression. </summary>
    public static double Out(double x) => Reverse(In, x);

    ///<summary> An easing function that represents an easing-in-out progression. </summary>
    public static double InOut(double x) => ToInOut(In, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuad"/>
    /// </summary>
    public static double QuadIn(double x) => x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuad"/>
    /// </summary>
    public static double QuadOut(double x) => Reverse(QuadIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuad"/>
    /// </summary>
    public static double QuadInOut(double x) => ToInOut(QuadIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInCubic"/>
    /// </summary>
    public static double CubicIn(double x) => x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutCubic"/>
    /// </summary>
    public static double CubicOut(double x) => Reverse(CubicIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutCubic"/>
    /// </summary>
    public static double CubicInOut(double x) => ToInOut(CubicIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuart"/>
    /// </summary>
    public static double QuartIn(double x) => x * x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuart"/>
    /// </summary>
    public static double QuartOut(double x) => Reverse(QuartIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuart"/>
    /// </summary>
    public static double QuartInOut(double x) => ToInOut(QuartIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuint"/>
    /// </summary>
    public static double QuintIn(double x) => x * x * x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuint"/>
    /// </summary>
    public static double QuintOut(double x) => Reverse(QuintIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuint"/>
    /// </summary>
    public static double QuintInOut(double x) => ToInOut(QuintIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInSine"/>
    /// </summary>
    public static double SineIn(double x) => 1 - Math.Cos(x * Math.PI * .5);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutSine"/>
    /// </summary>
    public static double SineOut(double x) => Reverse(SineIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutSine"/>
    /// </summary>
    public static double SineInOut(double x) => ToInOut(SineIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInExpo"/>
    /// </summary>
    public static double ExpoIn(double x) => Math.Pow(2, 10 * (x - 1));

    /// <summary>
    ///     <see href="https://easings.net/#easeOutExpo"/>
    /// </summary>
    public static double ExpoOut(double x) => Reverse(ExpoIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutExpo"/>
    /// </summary>
    public static double ExpoInOut(double x) => ToInOut(ExpoIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInCirc"/>
    /// </summary>
    public static double CircIn(double x) => 1 - Math.Sqrt(1 - x * x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutCirc"/>
    /// </summary>
    public static double CircOut(double x) => Reverse(CircIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutCirc"/>
    /// </summary>
    public static double CircInOut(double x) => ToInOut(CircIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInBack"/>
    /// </summary>
    public static double BackIn(double x) => x * x * ((1.70158 + 1) * x - 1.70158);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutBack"/>
    /// </summary>
    public static double BackOut(double x) => Reverse(BackIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutBack"/>
    /// </summary>
    public static double BackInOut(double x) => ToInOut(y => y * y * ((1.70158 * 1.525 + 1) * y - 1.70158 * 1.525), x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInBounce"/>
    /// </summary>
    public static double BounceIn(double x) => Reverse(BounceOut, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutBounce"/>
    /// </summary>
    public static double BounceOut(double x) => x < 1 / 2.75 ? 7.5625 * x * x :
        x < 2 / 2.75 ? 7.5625 * (x -= 1.5 / 2.75) * x + .75 :
        x < 2.5 / 2.75 ? 7.5625 * (x -= 2.25 / 2.75) * x + .9375 : 7.5625 * (x -= 2.625 / 2.75) * x + .984375;

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutBounce"/>
    /// </summary>
    public static double BounceInOut(double x) => ToInOut(BounceIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInElastic"/>
    /// </summary>
    public static double ElasticIn(double x) => Reverse(ElasticOut, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutElastic"/>
    /// </summary>
    public static double ElasticOut(double x) => Math.Pow(2, -10 * x) * Math.Sin((x - .075) * Math.Tau / .3) + 1;

    /// <summary/>
    public static double ElasticOutHalf(double x) => Math.Pow(2, -10 * x) * Math.Sin((.5 * x - .075) * Math.Tau / .3) + 1;

    /// <summary/>
    public static double ElasticOutQuarter(double x) => Math.Pow(2, -10 * x) * Math.Sin((.25 * x - .075) * Math.Tau / .3) + 1;

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutElastic"/>
    /// </summary>
    public static double ElasticInOut(double x) => ToInOut(ElasticIn, x);

    ///<summary> Reverses an easing function. </summary>
    public static double Reverse(Func<double, double> func, double value) => 1 - func(1 - value);

    ///<summary> Converts an easing function to its in-out counterpart. </summary>
    public static double ToInOut(Func<double, double> func, double value)
        => (value < .5 ? func(2 * value) : 2 - func(2 - 2 * value)) * .5;

    /// <summary> Applies the specified <see cref="OsbEasing"/> to the progress (<paramref name="value"/>). </summary>
    public static double Ease(this OsbEasing easing, double value) => ToEasingFunction(easing)(value);

    /// <summary> Converts an <see cref="OsbEasing"/> to one of the corresponding <see cref="EasingFunctions"/>. </summary>
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
        _ => Linear
    };
}