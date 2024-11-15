namespace StorybrewCommon.Storyboarding;

using System;

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

        return dotIndex < 0 ? $"{span}{GetFrameAt(time)}" : $"{span[..dotIndex]}{GetFrameAt(time)}{span[dotIndex..]}";
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