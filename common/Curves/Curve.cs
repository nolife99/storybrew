using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Curves
{
    ///<summary> Represents types of curves. </summary>
    public interface Curve
    {
        ///<summary> The start position (the head) of the curve. </summary>
        CommandPosition StartPosition { get; }

        ///<summary> The end position (the tail) of the curve. </summary>
        CommandPosition EndPosition { get; }

        ///<summary> The total length of the curve from the head to the tail. </summary>
        double Length { get; }

        ///<summary> Returns the position of the curve at <paramref name="distance"/>. </summary>
        CommandPosition PositionAtDistance(double distance);

        ///<summary> Returns the position of the curve at <paramref name="delta"/>. </summary>
        CommandPosition PositionAtDelta(double delta);
    }
}