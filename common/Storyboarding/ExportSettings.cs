namespace StorybrewCommon.Storyboarding;

using System.Globalization;

#pragma warning disable CS1591
public class ExportSettings
{
    public static readonly ExportSettings Default = new();
    public readonly NumberFormatInfo NumberFormat = CultureInfo.InvariantCulture.NumberFormat;

    public bool UseFloatForMove = true, UseFloatForTime;
}