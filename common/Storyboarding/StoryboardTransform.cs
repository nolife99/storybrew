namespace StorybrewCommon.Storyboarding;

using System;
using System.Numerics;
using Microsoft.Extensions.ObjectPool;
using StorybrewCommon.Util;

/// <summary>
///     A transform that applies to a storyboard element.
/// </summary>
public class StoryboardTransform : IResettable, IDisposable
{
    Affine2 transform;
    float transformScale, transformAngle;

    static readonly ObjectPool<StoryboardTransform> pool = ObjectPool.Create<StoryboardTransform>();

    /// <summary>
    ///     Initializes a new <see cref="StoryboardTransform"/> instance.
    /// </summary>
    /// <param name="parent">The parent transform to inherit from.</param>
    /// <param name="origin">The origin of the element.</param>
    /// <param name="position">The position of the element.</param>
    /// <param name="rotation">The rotation of the element in radians.</param>
    /// <param name="scale">The scale of the element.</param>
    public static StoryboardTransform Get(StoryboardTransform parent, Vector2 origin, Vector2 position, float rotation, float scale)
    {
        var instance = pool.Get();
        var transform = instance.transform = parent?.transform ?? Affine2.Identity;

        if (position != Vector2.Zero) transform.Translate(position.X, position.Y);
        if (rotation != 0) transform.Rotate(rotation);
        if (scale != 1) transform.Scale(scale, scale);
        if (origin != Vector2.Zero) transform.Translate(-origin.X, -origin.Y);

        instance.transformScale = (parent?.transformScale ?? 1) * scale;

        // https://math.stackexchange.com/questions/13150/extracting-rotation-scale-values-from-2d-transformation-matrix/13165#13165
        instance.transformAngle = MathF.Atan2(-transform.M21, transform.M11);
        // OR MathF.Atan2(-transform.M22, transform.M12);

        return instance;
    }

    bool IResettable.TryReset()
    {
        transform = Affine2.Identity;
        transformScale = transformAngle = 0;
        return true;
    }

    public void Dispose() => pool.Return(this);

    /// <summary>
    ///     Applies the transform to a position.
    /// </summary>
    /// <param name="value">The position to apply the transform to.</param>
    /// <returns>The transformed position.</returns>
    public Vector2 ApplyToPosition(Vector2 value) => transform.Transform(value);

    /// <summary>
    ///     Applies the transform to a position, separating the X and Y transformations.
    /// </summary>
    /// <param name="value">The position to apply the transform to.</param>
    /// <returns>The transformed position.</returns>
    public Vector2 ApplyToPositionXY(Vector2 value) => transform.TransformSeparate(value);

    /// <summary>
    ///     Applies the transform to a position's X component.
    /// </summary>
    /// <param name="value">The position's X component to apply the transform to.</param>
    /// <returns>The transformed position's X component.</returns>
    public float ApplyToPositionX(float value) => transform.TransformX(value);

    /// <summary>
    ///     Applies the transform to a position's Y component.
    /// </summary>
    /// <param name="value">The position's Y component to apply the transform to.</param>
    /// <returns>The transformed position's Y component.</returns>
    public float ApplyToPositionY(float value) => transform.TransformY(value);

    /// <summary>
    ///     Applies the transform to a rotation.
    /// </summary>
    /// <param name="value">The rotation to apply the transform to.</param>
    /// <returns>The transformed rotation.</returns>
    public float ApplyToRotation(float value) => value + transformAngle;

    /// <summary>
    ///     Applies the transform to a scale.
    /// </summary>
    /// <param name="value">The scale to apply the transform to.</param>
    /// <returns>The transformed scale.</returns>
    public float ApplyToScale(float value) => value * transformScale;

    /// <summary>
    ///     Applies the transform to a scale vector.
    /// </summary>
    /// <param name="value">The scale vector to apply the transform to.</param>
    /// <returns>The transformed scale vector.</returns>
    public Vector2 ApplyToScale(Vector2 value) => value * transformScale;
}