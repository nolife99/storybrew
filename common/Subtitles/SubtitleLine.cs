namespace StorybrewCommon.Subtitles;

/// <summary> Interpreted lines for subtitle files. </summary>
/// <remarks> Constructs a <see cref="SubtitleLine"/>. </remarks>
/// <param name="StartTime"> The start time of the subtitle line. </param>
/// <param name="EndTime"> The end time of the subtitle line. </param>
/// <param name="Text"> The text in the subtitle line. </param>
public record struct SubtitleLine(float StartTime, float EndTime, string Text);