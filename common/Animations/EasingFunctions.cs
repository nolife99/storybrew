﻿namespace StorybrewCommon.Animations;

using System;
using Storyboarding;

///<summary> A static class providing keyframing easing functions. </summary>
public static class EasingFunctions
{
    ///<summary> An easing function that represents the floor value of the progression. </summary>
    public static float Step(float x) => x >= 1 ? 1 : 0;

    ///<summary> An easing function that represents a linear progression. </summary>
    public static float Linear(float x) => x;

    ///<summary> An easing function that represents an easing-in progression. </summary>
    public static float In(float x) => float.Pow(x, 1.5f);

    ///<summary> An easing function that represents an easing-out progression. </summary>
    public static float Out(float x) => Reverse(In, x);

    ///<summary> An easing function that represents an easing-in-out progression. </summary>
    public static float InOut(float x) => ToInOut(In, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuad"/>
    /// </summary>
    public static float QuadIn(float x) => x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuad"/>
    /// </summary>
    public static float QuadOut(float x) => Reverse(QuadIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuad"/>
    /// </summary>
    public static float QuadInOut(float x) => ToInOut(QuadIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInCubic"/>
    /// </summary>
    public static float CubicIn(float x) => x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutCubic"/>
    /// </summary>
    public static float CubicOut(float x) => Reverse(CubicIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutCubic"/>
    /// </summary>
    public static float CubicInOut(float x) => ToInOut(CubicIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuart"/>
    /// </summary>
    public static float QuartIn(float x) => x * x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuart"/>
    /// </summary>
    public static float QuartOut(float x) => Reverse(QuartIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuart"/>
    /// </summary>
    public static float QuartInOut(float x) => ToInOut(QuartIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuint"/>
    /// </summary>
    public static float QuintIn(float x) => x * x * x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuint"/>
    /// </summary>
    public static float QuintOut(float x) => Reverse(QuintIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuint"/>
    /// </summary>
    public static float QuintInOut(float x) => ToInOut(QuintIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInSine"/>
    /// </summary>
    public static float SineIn(float x) => 1 - float.Cos(x * float.Pi * .5f);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutSine"/>
    /// </summary>
    public static float SineOut(float x) => Reverse(SineIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutSine"/>
    /// </summary>
    public static float SineInOut(float x) => ToInOut(SineIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInExpo"/>
    /// </summary>
    public static float ExpoIn(float x) => float.Pow(2, 10 * (x - 1));

    /// <summary>
    ///     <see href="https://easings.net/#easeOutExpo"/>
    /// </summary>
    public static float ExpoOut(float x) => Reverse(ExpoIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutExpo"/>
    /// </summary>
    public static float ExpoInOut(float x) => ToInOut(ExpoIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInCirc"/>
    /// </summary>
    public static float CircIn(float x) => 1 - float.Sqrt(1 - x * x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutCirc"/>
    /// </summary>
    public static float CircOut(float x) => Reverse(CircIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutCirc"/>
    /// </summary>
    public static float CircInOut(float x) => ToInOut(CircIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInBack"/>
    /// </summary>
    public static float BackIn(float x) => x * x * (2.70158f * x - 1.70158f);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutBack"/>
    /// </summary>
    public static float BackOut(float x) => Reverse(BackIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutBack"/>
    /// </summary>
    public static float BackInOut(float x) => ToInOut(y => y * y * ((1.70158f * 1.525f + 1) * y - 1.70158f * 1.525f), x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInBounce"/>
    /// </summary>
    public static float BounceIn(float x) => Reverse(BounceOut, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutBounce"/>
    /// </summary>
    public static float BounceOut(float x) => x < 1 / 2.75f ? 7.5625f * x * x :
        x < 2 / 2.75f ? 7.5625f * (x -= 1.5f / 2.75f) * x + .75f :
        x < 2.5f / 2.75f ? 7.5625f * (x -= 2.25f / 2.75f) * x + .9375f : 7.5625f * (x -= 2.625f / 2.75f) * x + .984375f;

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutBounce"/>
    /// </summary>
    public static float BounceInOut(float x) => ToInOut(BounceIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInElastic"/>
    /// </summary>
    public static float ElasticIn(float x) => Reverse(ElasticOut, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutElastic"/>
    /// </summary>
    public static float ElasticOut(float x) => float.Pow(2, -10 * x) * float.Sin((x - .075f) * float.Tau / .3f) + 1;

    /// <summary/>
    public static float ElasticOutHalf(float x) => float.Pow(2, -10 * x) * float.Sin((.5f * x - .075f) * float.Tau / .3f) + 1;

    /// <summary/>
    public static float ElasticOutQuarter(float x) => float.Pow(2, -10 * x) * float.Sin((.25f * x - .075f) * float.Tau / .3f) + 1;

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutElastic"/>
    /// </summary>
    public static float ElasticInOut(float x) => ToInOut(ElasticIn, x);

    ///<summary> Reverses an easing function. </summary>
    public static float Reverse(Func<float, float> func, float value) => 1 - func(1 - value);

    ///<summary> Converts an easing function to its in-out counterpart. </summary>
    public static float ToInOut(Func<float, float> func, float value)
        => (value < .5f ? func(2 * value) : 2 - func(2 - 2 * value)) * .5f;

    /// <summary> Applies the specified <see cref="OsbEasing"/> to the progress (<paramref name="value"/>). </summary>
    public static float Ease(this OsbEasing easing, float value) => ToEasingFunction(easing)(value);

    /// <summary> Converts an <see cref="OsbEasing"/> to one of the corresponding <see cref="EasingFunctions"/>. </summary>
    public static Func<float, float> ToEasingFunction(OsbEasing easing) => easing switch
    {
        OsbEasing.In => In,
        OsbEasing.Out => Out,
        OsbEasing.InQuad => QuadIn,
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