namespace StorybrewCommon.Storyboarding;

using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary>
///     A transform that applies to a storyboard element.
/// </summary>
public readonly struct StoryboardTransform
{
    /// <summary>
    ///     The identity transform.
    /// </summary>
    public static readonly StoryboardTransform Identity = Unsafe.BitCast<Matrix3x2, StoryboardTransform>(Matrix3x2.Identity);

    readonly Matrix3x2 transform = Matrix3x2.Identity;

    /// <summary>
    ///     Determines if the transform is the identity transform.
    /// </summary>
    public bool IsIdentity => transform == Matrix3x2.Identity;

    /// <summary>
    ///     Initializes a new <see cref="StoryboardTransform"/> instance.
    /// </summary>
    /// <param name="parent">The parent transform to inherit from.</param>
    /// <param name="origin">The origin of the element.</param>
    /// <param name="position">The position of the element.</param>
    /// <param name="rotation">The rotation of the element in radians.</param>
    /// <param name="scale">The scale of the element.</param>
    public StoryboardTransform(StoryboardTransform parent, Vector2 origin, Vector2 position, float rotation, float scale)
    {
        var transform = parent.transform;

        if (position != Vector2.Zero) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateTranslation(position));
        if (rotation != 0) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateRotation(rotation));
        if (scale != 1) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateScale(scale));
        if (origin != Vector2.Zero) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateTranslation(-origin.X, -origin.Y));

        this.transform = transform;
    }

    /// <summary>
    ///     Applies the transform to a position vector.
    /// </summary>
    public Vector2 ApplyToPosition(Vector2 value) => Vector2.Transform(value, transform);

    /// <summary>
    ///     Applies the transform to a position vector, separating the X and Y transformations.
    /// </summary>
    public Vector2 ApplyToPositionXY(Vector2 value) => new(Vector2.Transform(value with { Y = 0 }, transform).X,
        Vector2.Transform(value with { X = 0 }, transform).Y);

    /// <summary>
    ///     Applies the transform to a position's X component.
    /// </summary>
    public float ApplyToPositionX(float value) => Vector2.Transform(new(value, 0), transform).X;

    /// <summary>
    ///     Applies the transform to a position's Y component.
    /// </summary>
    public float ApplyToPositionY(float value) => Vector2.Transform(new(0, value), transform).Y;

    /// <summary>
    ///     Applies the transform to a rotation scalar.
    /// </summary>
    public float ApplyToRotation(float value) => value + float.Atan2(-transform.M21, transform.M11);

    /// <summary>
    ///     Applies the transform to a scale scalar.
    /// </summary>
    public float ApplyToScale(float value) => value * float.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);

    /// <summary>
    ///     Applies the transform to a scale vector.
    /// </summary>
    public Vector2 ApplyToScale(Vector2 value)
        => value * float.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);
}