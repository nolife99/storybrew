namespace StorybrewCommon.Storyboarding;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary> A type of <see cref="OsbSprite"/> that loops through given frames, or animates. </summary>
public class OsbAnimation : OsbSprite
{
    readonly Dictionary<int, string> frameMap = new();

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
        var frame = GetFrameAt(time);
        if (frameMap.TryGetValue(frame, out var result)) return result;

        var span = TexturePath.AsSpan();
        var dotIndex = span.LastIndexOf('.');
        var digits = StringHelper.GetDigitCount(frame);

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

        return frameMap[frame] = chars.ToString();
    }

    int GetFrameAt(float time)
    {
        var frame = (time - StartTime) / FrameDelay;
        switch (LoopType)
        {
            case OsbLoopType.LoopForever: frame %= FrameCount; break;
            case OsbLoopType.LoopOnce: frame = float.Min(frame, FrameCount - 1); break;
        }

        return int.Max(0, (int)frame);
    }

    private protected override void WriteHeader(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        StoryboardTransform transform)
    {
        writer.Write("Animation,");
        WriteHeaderCommon(writer, exportSettings, layer, transform);

        using var builder = StringHelper.Interpolate(exportSettings.NumberFormat,
            $",{FrameCount},{FrameDelay},{LoopType}");

        writer.WriteLine(builder.AsReadOnlySpan());
    }
}