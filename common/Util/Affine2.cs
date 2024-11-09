namespace StorybrewCommon.Util;

using System;
using System.Numerics;

#pragma warning disable CS1591
public struct Affine2(Vector3 row0, Vector3 row1) : IEquatable<Affine2>
{
    public static readonly Affine2 Identity = new(Vector3.UnitX, Vector3.UnitY);
    public Vector3 Row0 = row0, Row1 = row1;

    public float M11 { readonly get => Row0.X; set => Row0.X = value; }

    public float M21 { readonly get => Row0.Y; set => Row0.Y = value; }

    public float M31 { readonly get => Row0.Z; set => Row0.Z = value; }

    public float M12 { readonly get => Row1.X; set => Row1.X = value; }

    public float M22 { readonly get => Row1.Y; set => Row1.Y = value; }

    public float M32 { readonly get => Row1.Z; set => Row1.Z = value; }

    public void Translate(float x, float y)
    {
        Row0.Z += Row0.X * x + Row0.Y * y;
        Row1.Z += Row1.X * x + Row1.Y * y;
    }

    public void Scale(float x, float y)
    {
        Row0.X *= x;
        Row0.Y *= y;

        Row1.X *= x;
        Row1.Y *= y;
    }

    public void Rotate(float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);

        var row0 = Row0;
        var row1 = Row1;

        Row0.X = row0.X * cos + row0.Y * sin;
        Row0.Y = row0.X * -sin + row0.Y * cos;

        Row1.X = row1.X * cos + row1.Y * sin;
        Row1.Y = row1.X * -sin + row1.Y * cos;
    }

    public readonly Vector2 Transform(in Vector2 vector) => new(Row0.X * vector.X + Row0.Y * vector.Y + Row0.Z,
        Row1.X * vector.X + Row1.Y * vector.Y + Row1.Z);

    public readonly Vector2 TransformSeparate(in Vector2 vector) => new(Row0.X * vector.X + Row0.Z, Row1.Y * vector.Y + Row1.Z);

    public readonly float TransformX(float value) => Row0.X * value + Row0.Z;
    public readonly float TransformY(float value) => Row1.Y * value + Row1.Z;

    public readonly bool Equals(Affine2 other) => Row0 == other.Row0 && Row1 == other.Row1;
    public static bool operator ==(Affine2 left, Affine2 right) => left.Equals(right);
    public static bool operator !=(Affine2 left, Affine2 right) => !(left == right);

    public override bool Equals(object other) => other is Affine2 affine && Equals(affine);
    public readonly override int GetHashCode() => HashCode.Combine(Row0, Row1);
    public readonly override string ToString() => $"{Row0} {Row1}";
}