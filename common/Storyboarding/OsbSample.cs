namespace StorybrewCommon.Storyboarding;

using System;
using System.IO;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary> A type of <see cref="StoryboardObject"/> that plays an audio file. </summary>
public class OsbSample : StoryboardObject
{
    string audioPath = "";

    ///<summary> The time of which this audio is played. </summary>
    public float Time;

    ///<summary> The volume (out of 100) of this audio sample. </summary>
    public float Volume = 100;

    ///<summary> Gets the audio path of this audio sample. </summary>
    public string AudioPath
    {
        get => audioPath;
        set
        {
            if (audioPath == value) return;

            audioPath = value;
        }
    }

    /// <inheritdoc/>
    public override float StartTime => Time;

    /// <inheritdoc/>
    public override float EndTime => Time;

    /// <summary/>
    public override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        OsbLayer layer,
        scoped ref readonly StoryboardTransform transform)
    {
        using var str = StringHelper.Interpolate(exportSettings.NumberFormat,
            $"Sample,{float.ConvertToIntegerNative<int>(Time)},{layer},\"{audioPath.AsSpan().Trim()}\",{(int)Volume}");

        writer.WriteLine(str.AsReadOnlySpan());
    }
}