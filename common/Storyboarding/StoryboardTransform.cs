namespace StorybrewCommon.Storyboarding;

using System;
using System.Numerics;
using StorybrewCommon.Util;

public class StoryboardTransform
{
    readonly Affine2 transform;
    readonly float transformScale, transformAngle;

    public StoryboardTransform(StoryboardTransform parent, Vector2 origin, Vector2 position, float rotation,
        float scale)
    {
        transform = parent?.transform ?? Affine2.Identity;
        if (position != Vector2.Zero) transform.Translate(position.X, position.Y);
        if (rotation != 0) transform.Rotate(rotation);
        if (scale != 1) transform.Scale(scale, scale);
        if (origin != Vector2.Zero) transform.Translate(-origin.X, -origin.Y);

        transformScale = (parent?.transformScale ?? 1) * scale;

        // https://math.stackexchange.com/questions/13150/extracting-rotation-scale-values-from-2d-transformation-matrix/13165#13165
        transformAngle = MathF.Atan2(-transform.M21, transform.M11); // OR MathF.Atan2(-transform.M22, transform.M12);
    }

    public Vector2 ApplyToPosition(Vector2 value) => transform.Transform(value);
    public Vector2 ApplyToPositionXY(Vector2 value) => transform.TransformSeparate(value);
    public float ApplyToPositionX(float value) => transform.TransformX(value);
    public float ApplyToPositionY(float value) => transform.TransformY(value);
    public float ApplyToRotation(float value) => value + transformAngle;
    public float ApplyToScale(float value) => value * transformScale;
    public Vector2 ApplyToScale(Vector2 value) => value * transformScale;
}