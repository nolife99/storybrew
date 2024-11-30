﻿namespace StorybrewCommon.Storyboarding;

using System;
using System.Numerics;
using BrewLib.Util;

/// <summary>
///     A transform that applies to a storyboard element.
/// </summary>
public class StoryboardTransform : IDisposable
{
    static readonly Pool<StoryboardTransform> pool = new(obj =>
    {
        obj.transform = Matrix3x2.Identity;
        obj.transformScale = obj.transformAngle = 0;
    });

    Matrix3x2 transform;
    float transformScale, transformAngle;

    /// <inheritdoc/>
    public void Dispose() => pool.Release(this);

    /// <summary>
    ///     Initializes a new <see cref="StoryboardTransform"/> instance.
    /// </summary>
    /// <param name="parent">The parent transform to inherit from.</param>
    /// <param name="origin">The origin of the element.</param>
    /// <param name="position">The position of the element.</param>
    /// <param name="rotation">The rotation of the element in radians.</param>
    /// <param name="scale">The scale of the element.</param>
    public static StoryboardTransform Get(StoryboardTransform parent,
        Vector2 origin,
        Vector2 position,
        float rotation,
        float scale)
    {
        var instance = pool.Retrieve();
        var transform = parent?.transform ?? Matrix3x2.Identity;

        if (position != Vector2.Zero) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateTranslation(position));
        if (rotation != 0) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateRotation(rotation));
        if (scale != 1) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateScale(scale, scale));
        if (origin != Vector2.Zero) transform = Matrix3x2.Multiply(transform, Matrix3x2.CreateTranslation(-origin.X, -origin.Y));

        instance.transform = transform;
        instance.transformScale = (parent?.transformScale ?? 1) * scale;
        instance.transformAngle = float.Atan2(-transform.M21, transform.M11);

        return instance;
    }

    /// <summary>
    ///     Applies the transform to a position.
    /// </summary>
    /// <param name="value">The position to apply the transform to.</param>
    /// <returns>The transformed position.</returns>
    public Vector2 ApplyToPosition(Vector2 value) => Vector2.Transform(value, transform);

    /// <summary>
    ///     Applies the transform to a position, separating the X and Y transformations.
    /// </summary>
    /// <param name="value">The position to apply the transform to.</param>
    /// <returns>The transformed position.</returns>
    public Vector2 ApplyToPositionXY(Vector2 value) => new(Vector2.Transform(value with { Y = 0 }, transform).X,
        Vector2.Transform(value with { X = 0 }, transform).Y);

    /// <summary>
    ///     Applies the transform to a position's X component.
    /// </summary>
    /// <param name="value">The position's X component to apply the transform to.</param>
    /// <returns>The transformed position's X component.</returns>
    public float ApplyToPositionX(float value) => Vector2.Transform(new(value, 0), transform).X;

    /// <summary>
    ///     Applies the transform to a position's Y component.
    /// </summary>
    /// <param name="value">The position's Y component to apply the transform to.</param>
    /// <returns>The transformed position's Y component.</returns>
    public float ApplyToPositionY(float value) => Vector2.Transform(new(0, value), transform).Y;

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