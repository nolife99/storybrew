namespace StorybrewCommon.Subtitles;

/// <summary> Interpreted lines for subtitle files. </summary>
/// <remarks> Constructs a <see cref="SubtitleLine" />. </remarks>
/// <param name="startTime"> The start time of the subtitle line. </param>
/// <param name="endTime"> The end time of the subtitle line. </param>
/// <param name="text"> The text in the subtitle line. </param>
public struct SubtitleLine(float startTime, float endTime, string text)
{
    ///<summary> The start time of the subtitle line. </summary>
    public float StartTime = startTime;

    ///<summary> The end time of the subtitle line. </summary>
    public float EndTime = endTime;

    ///<summary> The text in the subtitle line. </summary>
    public string Text = text;
}