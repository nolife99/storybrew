namespace StorybrewCommon.Storyboarding;

using System.IO;

/// <summary> A type of <see cref="StoryboardObject" /> that plays an audio file. </summary>
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

    /// <inheritdoc />
    public override float StartTime => Time;

    /// <inheritdoc />
    public override float EndTime => Time;

    /// <summary />
    public override void WriteOsb(TextWriter writer, ExportSettings exportSettings, OsbLayer layer,
        StoryboardTransform transform)
        => writer.WriteLine($"Sample,{((int)Time).ToString(exportSettings.NumberFormat)},{layer},\"{AudioPath.Trim()
        }\",{((int)Volume).ToString(exportSettings.NumberFormat)}");
}