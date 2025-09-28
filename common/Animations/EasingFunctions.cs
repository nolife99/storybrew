namespace StorybrewCommon.Animations;

using System;
using System.Runtime.CompilerServices;
using StorybrewCommon.Storyboarding;

///<summary> A static class providing keyframing easing functions. </summary>
public static class EasingFunctions
{
    ///<summary> An easing function that represents the integer value of the progression. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Step(float x) => x >= 1 ? 1 : 0;

    ///<summary> An easing function that represents a linear progression. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Linear(float x) => x;

    ///<summary> An easing function that represents an easing-in progression. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float In(float x) => QuadIn(x);

    ///<summary> An easing function that represents an easing-out progression. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Out(float x) => QuadOut(x);

    /// <summary>
    /// <see href="https://easings.net/#easeInQuad"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuadIn(float x) => x * x;

    /// <summary>
    /// <see href="https://easings.net/#easeOutQuad"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuadOut(float x) => 1 - QuadIn(1 - x);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutQuad"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuadInOut(float x) => (x < .5f ? QuadIn(2 * x) : 2 - QuadIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInCubic"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubicIn(float x) => x * x * x;

    /// <summary>
    /// <see href="https://easings.net/#easeOutCubic"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubicOut(float x) => 1 - CubicIn(1 - x);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutCubic"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubicInOut(float x) => (x < .5f ? CubicIn(2 * x) : 2 - CubicIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInQuart"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuartIn(float x) => x * x * x * x;

    /// <summary>
    /// <see href="https://easings.net/#easeOutQuart"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuartOut(float x) => 1 - QuartIn(1 - x);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutQuart"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuartInOut(float x) => (x < .5f ? QuartIn(2 * x) : 2 - QuartIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInQuint"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuintIn(float x) => x * x * x * x * x;

    /// <summary>
    /// <see href="https://easings.net/#easeOutQuint"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuintOut(float x) => 1 - QuintIn(1 - x);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutQuint"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuintInOut(float x) => (x < .5f ? QuintIn(2 * x) : 2 - QuintIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInSine"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SineIn(float x) => 1 - float.Cos(x * float.Pi * .5f);

    /// <summary>
    /// <see href="https://easings.net/#easeOutSine"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SineOut(float x) => float.Sin(x * float.Pi * .5f);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutSine"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SineInOut(float x) => -(float.Cos(x * float.Pi) - 1) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInExpo"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ExpoIn(float x) => x == 0 ? 0 : float.Pow(2, 10 * x - 10);

    /// <summary>
    /// <see href="https://easings.net/#easeOutExpo"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ExpoOut(float x) => x == 1 ? 1 : 1 - float.Pow(2, -10 * x);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutExpo"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ExpoInOut(float x) => (x < .5f ? ExpoIn(2 * x) : 2 - ExpoIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInCirc"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CircIn(float x) => 1 - float.Sqrt(1 - x * x);

    /// <summary>
    /// <see href="https://easings.net/#easeOutCirc"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CircOut(float x) => float.Sqrt(1 - QuadIn(x - 1));

    /// <summary>
    /// <see href="https://easings.net/#easeInOutCirc"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CircInOut(float x) => (x < .5f ? CircIn(2 * x) : 2 - CircIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInBack"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BackIn(float x) => x * x * (2.70158f * x - 1.70158f);

    /// <summary>
    /// <see href="https://easings.net/#easeOutBack"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BackOut(float x) => 1 + 2.70158f * CubicIn(x - 1) + 1.70158f * QuadIn(x - 1);

    /// <summary>
    /// <see href="https://easings.net/#easeInOutBack"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BackInOut(float x)
        => ToInOut(y => y * y * ((1.70158f * 1.525f + 1) * y - 1.70158f * 1.525f), x);

    /// <summary>
    /// <see href="https://easings.net/#easeInBounce"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BounceIn(float x) => 1 - BounceOut(1 - x);

    /// <summary>
    /// <see href="https://easings.net/#easeOutBounce"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BounceOut(float x)
        => x < 1 / 2.75f ? 7.5625f * x * x :
            x < 2 / 2.75f ? 7.5625f * (x -= 1.5f / 2.75f) * x + .75f :
            x < 2.5f / 2.75f ? 7.5625f * (x -= 2.25f / 2.75f) * x + .9375f :
            7.5625f * (x -= 2.625f / 2.75f) * x + .984375f;

    /// <summary>
    /// <see href="https://easings.net/#easeInOutBounce"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float BounceInOut(float x) => (x < .5f ? BounceIn(2 * x) : 2 - BounceIn(2 - 2 * x)) * .5f;

    /// <summary>
    /// <see href="https://easings.net/#easeInElastic"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ElasticIn(float x) => 1 - ElasticOut(1 - x);

    /// <summary>
    /// <see href="https://easings.net/#easeOutElastic"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ElasticOut(float x) => float.Pow(2, -10 * x) * float.Sin((x - .075f) * float.Tau / .3f) + 1;

    /// <summary/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ElasticOutHalf(float x)
        => float.Pow(2, -10 * x) * float.Sin((.5f * x - .075f) * float.Tau / .3f) + 1;

    /// <summary/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ElasticOutQuarter(float x)
        => float.Pow(2, -10 * x) * float.Sin((.25f * x - .075f) * float.Tau / .3f) + 1;

    /// <summary>
    /// <see href="https://easings.net/#easeInOutElastic"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ElasticInOut(float x) => (x < .5f ? ElasticIn(2 * x) : 2 - ElasticIn(2 - 2 * x)) * .5f;

    ///<summary> Reverses an easing function. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Reverse(Func<float, float> func, float value) => 1 - func(1 - value);

    ///<summary> Converts an easing function to its in-out counterpart. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToInOut(Func<float, float> func, float value)
        => (value < .5f ? func(2 * value) : 2 - func(2 - 2 * value)) * .5f;

    /// <summary> Applies the specified <see cref="OsbEasing"/> to the progress (<paramref name="value"/>). </summary>
    public static float Ease(this OsbEasing easing, float value)
        => easing switch
        {
            OsbEasing.In or OsbEasing.InQuad => QuadIn(value),
            OsbEasing.Out or OsbEasing.OutQuad => QuadOut(value),
            OsbEasing.InOutQuad => QuadInOut(value),
            OsbEasing.InCubic => CubicIn(value),
            OsbEasing.OutCubic => CubicOut(value),
            OsbEasing.InOutCubic => CubicInOut(value),
            OsbEasing.InQuart => QuartIn(value),
            OsbEasing.OutQuart => QuartOut(value),
            OsbEasing.InOutQuart => QuartInOut(value),
            OsbEasing.InQuint => QuintIn(value),
            OsbEasing.OutQuint => QuintOut(value),
            OsbEasing.InOutQuint => QuintInOut(value),
            OsbEasing.InSine => SineIn(value),
            OsbEasing.OutSine => SineOut(value),
            OsbEasing.InOutSine => SineInOut(value),
            OsbEasing.InExpo => ExpoIn(value),
            OsbEasing.OutExpo => ExpoOut(value),
            OsbEasing.InOutExpo => ExpoInOut(value),
            OsbEasing.InCirc => CircIn(value),
            OsbEasing.OutCirc => CircOut(value),
            OsbEasing.InOutCirc => CircInOut(value),
            OsbEasing.InElastic => ElasticIn(value),
            OsbEasing.OutElastic => ElasticOut(value),
            OsbEasing.OutElasticHalf => ElasticOutHalf(value),
            OsbEasing.OutElasticQuarter => ElasticOutQuarter(value),
            OsbEasing.InOutElastic => ElasticInOut(value),
            OsbEasing.InBack => BackIn(value),
            OsbEasing.OutBack => BackOut(value),
            OsbEasing.InOutBack => BackInOut(value),
            OsbEasing.InBounce => BounceIn(value),
            OsbEasing.OutBounce => BounceOut(value),
            OsbEasing.InOutBounce => BounceInOut(value),
            _ => Linear(value)
        };

    /// <summary> Converts an <see cref="OsbEasing"/> to one of the corresponding <see cref="EasingFunctions"/>. </summary>
    public static Func<float, float> ToEasingFunction(OsbEasing easing)
        => easing switch
        {
            OsbEasing.In or OsbEasing.InQuad => QuadIn,
            OsbEasing.Out or OsbEasing.OutQuad => QuadOut,
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