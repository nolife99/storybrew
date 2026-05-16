namespace StorybrewCommon.Storyboarding;

using System.Numerics;
using System.Runtime.CompilerServices;

/// <summary> A transform that applies to a storyboard element. </summary>
public readonly struct StoryboardTransform
{
    /// <summary> The identity transform. </summary>
    public static readonly StoryboardTransform Identity =
        Unsafe.BitCast<Matrix3x2, StoryboardTransform>(Matrix3x2.Identity);

    readonly Matrix3x2 transform = Matrix3x2.Identity;

    /// <summary> Determines if the transform is the identity transform. </summary>
    public bool IsIdentity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => transform == Matrix3x2.Identity;
    }

    /// <summary> Gets the underlying affine matrix. </summary>
    public Matrix3x2 Matrix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => transform;
    }

    /// <summary> Initializes a new <see cref="StoryboardTransform"/> instance. </summary>
    /// <param name="parent"> The parent transform to inherit from. </param>
    /// <param name="origin"> The origin of the element. </param>
    /// <param name="position"> The position of the element. </param>
    /// <param name="rotation"> The rotation of the element in radians. </param>
    /// <param name="scale"> The scale of the element. </param>
    /// <param name="flipX"> Whether to reflect the element along the X axis. </param>
    /// <param name="flipY"> Whether to reflect the element along the Y axis. </param>
    public StoryboardTransform(StoryboardTransform parent,
        Vector2 origin,
        Vector2 position,
        float rotation,
        float scale,
        bool flipX,
        bool flipY)
    {
        var localTransform = parent.transform;
        localTransform = Matrix3x2.Multiply(localTransform, Matrix3x2.CreateTranslation(position - origin));
        localTransform = Matrix3x2.Multiply(localTransform, Matrix3x2.CreateRotation(rotation));
        localTransform = Matrix3x2.Multiply(localTransform,
            Matrix3x2.CreateScale(flipX ? -scale : scale, flipY ? -scale : scale));

        transform = localTransform;
    }

    /// <summary> Applies the transform to a position vector. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ApplyToPosition(Vector2 value) => Vector2.Transform(value, transform);

    /// <summary> Applies the transform to a position vector, separating the X and Y transformations. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ApplyToPositionXY(Vector2 value)
        => new(Vector2.Transform(new(value.X, 0), transform).X, Vector2.Transform(new(0, value.Y), transform).Y);

    /// <summary> Applies the transform to a position's X component. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ApplyToPositionX(float value) => Vector2.Transform(new(value, 0), transform).X;

    /// <summary> Applies the transform to a position's Y component. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ApplyToPositionY(float value) => Vector2.Transform(new(0, value), transform).Y;

    /// <summary> Applies the transform to a rotation scalar. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ApplyToRotation(float value) => value + float.Atan2(-transform.M21, transform.M11);

    /// <summary> Applies the transform to a scale scalar. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ApplyToScale(float value)
        => value * float.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);

    /// <summary> Applies the transform to a scale vector. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ApplyToScale(Vector2 value)
        => value * float.Sqrt(transform.M11 * transform.M11 + transform.M12 * transform.M12);
}