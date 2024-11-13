namespace StorybrewCommon.Curves;

using System.Numerics;

/// <summary>
/// Represents a curve that can be queried for its length and sampled at points along its length.
/// </summary>
public interface Curve
{
    /// <summary>
    /// The start position of the curve.
    /// </summary>
    Vector2 StartPosition { get; }

    /// <summary>
    /// The end position of the curve.
    /// </summary>
    Vector2 EndPosition { get; }

    /// <summary>
    /// The total length of the curve.
    /// </summary>
    float Length { get; }

    /// <summary>
    /// Gets the position along the curve at the specified distance from the start.
    /// </summary>
    /// <param name="distance">The distance from the start of the curve.</param>
    /// <returns>The position at the specified distance.</returns>
    Vector2 PositionAtDistance(float distance);

    /// <summary>
    /// Gets the position along the curve at the specified fraction of its length.
    /// </summary>
    /// <param name="delta">The fraction of the curve's length, from 0 to 1.</param>
    /// <returns>The position at the specified fraction.</returns>
    Vector2 PositionAtDelta(float delta);
}