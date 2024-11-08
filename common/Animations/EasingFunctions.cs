namespace StorybrewCommon.Animations;

using System;
using Storyboarding;

///<summary> A static class providing keyframing easing functions. </summary>
public static class EasingFunctions
{
    ///<summary> An easing function that represents the floor value of the progression. </summary>
    public static readonly Func<float, float> Step = x => x >= 1 ? 1 : 0;

    ///<summary> An easing function that represents a linear progression. </summary>
    public static readonly Func<float, float> Linear = x => x;

    ///<summary> An easing function that represents an easing-in progression. </summary>
    public static readonly Func<float, float> In = x => MathF.Pow(x, 1.5f);

    ///<summary> An easing function that represents an easing-out progression. </summary>
    public static readonly Func<float, float> Out = x => Reverse(In, x);

    ///<summary> An easing function that represents an easing-in-out progression. </summary>
    public static readonly Func<float, float> InOut = x => ToInOut(In, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuad" />
    /// </summary>
    public static readonly Func<float, float> QuadIn = x => x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuad" />
    /// </summary>
    public static readonly Func<float, float> QuadOut = x => Reverse(QuadIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuad" />
    /// </summary>
    public static readonly Func<float, float> QuadInOut = x => ToInOut(QuadIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInCubic" />
    /// </summary>
    public static readonly Func<float, float> CubicIn = x => x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutCubic" />
    /// </summary>
    public static readonly Func<float, float> CubicOut = x => Reverse(CubicIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutCubic" />
    /// </summary>
    public static readonly Func<float, float> CubicInOut = x => ToInOut(CubicIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuart" />
    /// </summary>
    public static readonly Func<float, float> QuartIn = x => x * x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuart" />
    /// </summary>
    public static readonly Func<float, float> QuartOut = x => Reverse(QuartIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuart" />
    /// </summary>
    public static readonly Func<float, float> QuartInOut = x => ToInOut(QuartIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInQuint" />
    /// </summary>
    public static readonly Func<float, float> QuintIn = x => x * x * x * x * x;

    /// <summary>
    ///     <see href="https://easings.net/#easeOutQuint" />
    /// </summary>
    public static readonly Func<float, float> QuintOut = x => Reverse(QuintIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutQuint" />
    /// </summary>
    public static readonly Func<float, float> QuintInOut = x => ToInOut(QuintIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInSine" />
    /// </summary>
    public static readonly Func<float, float> SineIn = x => 1 - MathF.Cos(x * MathF.PI * .5f);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutSine" />
    /// </summary>
    public static readonly Func<float, float> SineOut = x => Reverse(SineIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutSine" />
    /// </summary>
    public static readonly Func<float, float> SineInOut = x => ToInOut(SineIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInExpo" />
    /// </summary>
    public static readonly Func<float, float> ExpoIn = x => MathF.Pow(2, 10 * (x - 1));

    /// <summary>
    ///     <see href="https://easings.net/#easeOutExpo" />
    /// </summary>
    public static readonly Func<float, float> ExpoOut = x => Reverse(ExpoIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutExpo" />
    /// </summary>
    public static readonly Func<float, float> ExpoInOut = x => ToInOut(ExpoIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInCirc" />
    /// </summary>
    public static readonly Func<float, float> CircIn = x => 1 - MathF.Sqrt(1 - x * x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutCirc" />
    /// </summary>
    public static readonly Func<float, float> CircOut = x => Reverse(CircIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutCirc" />
    /// </summary>
    public static readonly Func<float, float> CircInOut = x => ToInOut(CircIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInBack" />
    /// </summary>
    public static readonly Func<float, float> BackIn = x => x * x * ((1.70158f + 1) * x - 1.70158f);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutBack" />
    /// </summary>
    public static readonly Func<float, float> BackOut = x => Reverse(BackIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutBack" />
    /// </summary>
    public static readonly Func<float, float> BackInOut = x
        => ToInOut(y => y * y * ((1.70158f * 1.525f + 1) * y - 1.70158f * 1.525f), x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInBounce" />
    /// </summary>
    public static readonly Func<float, float> BounceIn = x => Reverse(BounceOut, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutBounce" />
    /// </summary>
    public static readonly Func<float, float> BounceOut = x => x < 1 / 2.75f ? 7.5625f * x * x :
        x < 2 / 2.75f ? 7.5625f * (x -= 1.5f / 2.75f) * x + .75f :
        x < 2.5f / 2.75f ? 7.5625f * (x -= 2.25f / 2.75f) * x + .9375f : 7.5625f * (x -= 2.625f / 2.75f) * x + .984375f;

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutBounce" />
    /// </summary>
    public static readonly Func<float, float> BounceInOut = x => ToInOut(BounceIn, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeInElastic" />
    /// </summary>
    public static readonly Func<float, float> ElasticIn = x => Reverse(ElasticOut, x);

    /// <summary>
    ///     <see href="https://easings.net/#easeOutElastic" />
    /// </summary>
    public static readonly Func<float, float> ElasticOut = x
        => MathF.Pow(2, -10 * x) * MathF.Sin((x - .075f) * MathF.Tau / .3f) + 1;

    /// <summary />
    public static readonly Func<float, float> ElasticOutHalf =
        x => MathF.Pow(2, -10 * x) * MathF.Sin((.5f * x - .075f) * MathF.Tau / .3f) + 1;

    /// <summary />
    public static readonly Func<float, float> ElasticOutQuarter =
        x => MathF.Pow(2, -10 * x) * MathF.Sin((.25f * x - .075f) * MathF.Tau / .3f) + 1;

    /// <summary>
    ///     <see href="https://easings.net/#easeInOutElastic" />
    /// </summary>
    public static readonly Func<float, float> ElasticInOut = x => ToInOut(ElasticIn, x);

    ///<summary> Reverses an easing function. </summary>
    public static float Reverse(Func<float, float> func, float value) => 1 - func(1 - value);

    ///<summary> Converts an easing function to its in-out counterpart. </summary>
    public static float ToInOut(Func<float, float> func, float value)
        => (value < .5f ? func(2 * value) : 2 - func(2 - 2 * value)) * .5f;

    /// <summary> Applies the specified <see cref="OsbEasing" /> to the progress (<paramref name="value" />). </summary>
    public static float Ease(this OsbEasing easing, float value) => ToEasingFunction(easing)(value);

    /// <summary> Converts an <see cref="OsbEasing" /> to one of the corresponding <see cref="EasingFunctions" />. </summary>
    public static Func<float, float> ToEasingFunction(OsbEasing easing)
        => easing switch
        {
            OsbEasing.In => In, OsbEasing.InQuad => QuadIn, OsbEasing.Out => Out, OsbEasing.OutQuad => QuadOut,
            OsbEasing.InOutQuad => QuadInOut, OsbEasing.InCubic => CubicIn, OsbEasing.OutCubic => CubicOut,
            OsbEasing.InOutCubic => CubicInOut, OsbEasing.InQuart => QuartIn, OsbEasing.OutQuart => QuartOut,
            OsbEasing.InOutQuart => QuartInOut, OsbEasing.InQuint => QuintIn, OsbEasing.OutQuint => QuintOut,
            OsbEasing.InOutQuint => QuintInOut, OsbEasing.InSine => SineIn, OsbEasing.OutSine => SineOut,
            OsbEasing.InOutSine => SineInOut, OsbEasing.InExpo => ExpoIn, OsbEasing.OutExpo => ExpoOut,
            OsbEasing.InOutExpo => ExpoInOut, OsbEasing.InCirc => CircIn, OsbEasing.OutCirc => CircOut,
            OsbEasing.InOutCirc => CircInOut, OsbEasing.InElastic => ElasticIn, OsbEasing.OutElastic => ElasticOut,
            OsbEasing.OutElasticHalf => ElasticOutHalf, OsbEasing.OutElasticQuarter => ElasticOutQuarter,
            OsbEasing.InOutElastic => ElasticInOut, OsbEasing.InBack => BackIn, OsbEasing.OutBack => BackOut,
            OsbEasing.InOutBack => BackInOut, OsbEasing.InBounce => BounceIn, OsbEasing.OutBounce => BounceOut,
            OsbEasing.InOutBounce => BounceInOut, _ => Linear
        };
}