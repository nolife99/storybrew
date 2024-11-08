namespace StorybrewCommon.Storyboarding;

using System.Globalization;

#pragma warning disable CS1591
public class ExportSettings
{
    public static readonly ExportSettings Default = new();

    public readonly NumberFormatInfo NumberFormat = CultureInfo.InvariantCulture.NumberFormat;

    /// <summary> Enables optimisation for sprites that have <see cref="OsbSprite.CommandSplitThreshold" /> > 0. </summary>
    public bool OptimiseSprites = true;

    public bool UseFloatForMove = true;
    public bool UseFloatForTime;
}