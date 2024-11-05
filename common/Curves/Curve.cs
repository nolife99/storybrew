using System.Numerics;

namespace StorybrewCommon.Curves;

///<summary> Represents types of curves. </summary>
public interface Curve
{
    ///<summary> The start position (the head) of the curve. </summary>
    Vector2 StartPosition { get; }

    ///<summary> The end position (the tail) of the curve. </summary>
    Vector2 EndPosition { get; }

    ///<summary> The total length of the curve from the head to the tail. </summary>
    float Length { get; }

    ///<summary> Returns the position of the curve at <paramref name="distance"/> pixels. </summary>
    Vector2 PositionAtDistance(float distance);

    ///<summary> Returns the position of the curve at <paramref name="delta"/>. </summary>
    Vector2 PositionAtDelta(float delta);
}