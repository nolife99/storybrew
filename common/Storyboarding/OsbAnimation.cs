namespace StorybrewCommon.Storyboarding;

using System;
using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;

/// <summary> A type of <see cref="OsbSprite"/> that loops through given frames, or animates. </summary>
public class OsbAnimation : OsbSprite
{
    ///<summary> Amount of frames in the animation. </summary>
    public int FrameCount;

    ///<summary> Delay between frames in the animation. </summary>
    public float FrameDelay;

    /// <summary> The <see cref="OsbLoopType"/> of this animation. </summary>
    public OsbLoopType LoopType;

    ///<summary> How long the animation takes to loop through its frames once. </summary>
    public float LoopDuration => FrameCount * FrameDelay;

    ///<summary> The time of when the animation stops looping. </summary>
    public float AnimationEndTime => LoopType is OsbLoopType.LoopOnce ? StartTime + LoopDuration : EndTime;

    /// <summary> Gets the path of the frame at <paramref name="time"/>. </summary>
    public override string GetTexturePathAt(float time)
    {
        var span = TexturePath.AsSpan();
        var dotIndex = span.LastIndexOf('.');
        var frame = GetFrameAt(time);
        var digits = frame == 0 ? 1 : (int)float.Floor(float.Log10(frame) + 1);

        Span<char> chars = stackalloc char[span.Length + digits];
        if (dotIndex < 0)
        {
            span.CopyTo(chars);
            frame.TryFormat(chars[span.Length..], out _, default, CultureInfo.InvariantCulture);
        }
        else
        {
            span[..dotIndex].CopyTo(chars);
            frame.TryFormat(chars[dotIndex..], out _, default, CultureInfo.InvariantCulture);
            span[dotIndex..].CopyTo(chars[(dotIndex + digits)..]);
        }

        return StringPool.Shared.GetOrAdd(chars);
    }

    int GetFrameAt(float time)
    {
        var frame = (time - StartTime) / FrameDelay;
        switch (LoopType)
        {
            case OsbLoopType.LoopForever: frame %= FrameCount; break;
            case OsbLoopType.LoopOnce: frame = Math.Min(frame, FrameCount - 1); break;
        }

        return Math.Max(0, (int)frame);
    }
}