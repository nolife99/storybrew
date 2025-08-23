namespace StorybrewCommon.Util;

using System.Numerics;
using SixLabors.ImageSharp;

#pragma warning disable CS1591
public readonly struct OrientedBoundingBox
{
    readonly Vector2 corner0, corner1, corner2, corner3, axis0, axis1;
    readonly float origin0, origin1;

    public OrientedBoundingBox(Vector2 position, Vector2 origin, Vector2 size, float angle)
    {
        var (sin, cos) = float.SinCos(angle);
        Vector2 unitRight = new(cos, sin), unitUp = new(-sin, cos);

        var right = unitRight * (size.X - origin.X);
        var up = unitUp * (size.Y - origin.Y);
        var left = unitRight * -origin.X;
        var down = unitUp * -origin.Y;

        corner0 = position + left + down;
        corner1 = position + right + down;
        corner2 = position + right + up;
        corner3 = position + left + up;

        axis0 = corner1 - corner0;
        axis1 = corner3 - corner0;

        axis0 /= axis0.LengthSquared();
        axis1 /= axis1.LengthSquared();

        origin0 = Vector2.Dot(corner0, axis0);
        origin1 = Vector2.Dot(corner0, axis1);
    }

    public RectangleF GetAABB()
        => RectangleF.FromLTRB(float.Min(float.Min(corner0.X, corner1.X), float.Min(corner2.X, corner3.X)),
            float.Min(float.Min(corner0.Y, corner1.Y), float.Min(corner2.Y, corner3.Y)),
            float.Max(float.Max(corner0.X, corner1.X), float.Max(corner2.X, corner3.X)),
            float.Max(float.Max(corner0.Y, corner1.Y), float.Max(corner2.Y, corner3.Y)));

    public bool Intersects(RectangleF other)
    {
        OrientedBoundingBox otherBox = new(other.Location, Vector2.Zero, other.Size, 0);
        return intersects1Way(in otherBox) && otherBox.intersects1Way(in this);
    }

    bool intersects1Way(scoped ref readonly OrientedBoundingBox other)
    {
        {
            var axis = axis0;
            var t = Vector2.Dot(other.corner0, axis);
            var tMin = t;
            var tMax = t;

            t = Vector2.Dot(other.corner1, axis);
            if (t < tMin) tMin = t;
            else if (t > tMax) tMax = t;

            t = Vector2.Dot(other.corner2, axis);
            if (t < tMin) tMin = t;
            else if (t > tMax) tMax = t;

            t = Vector2.Dot(other.corner3, axis);
            if (t < tMin) tMin = t;
            else if (t > tMax) tMax = t;

            if (tMin > 1 + origin0 || tMax < origin0) return false;
        }

        {
            var axis = axis1;
            var t = Vector2.Dot(other.corner0, axis);
            var tMin = t;
            var tMax = t;

            t = Vector2.Dot(other.corner1, axis);
            if (t < tMin) tMin = t;
            else if (t > tMax) tMax = t;

            t = Vector2.Dot(other.corner2, axis);
            if (t < tMin) tMin = t;
            else if (t > tMax) tMax = t;

            t = Vector2.Dot(other.corner3, axis);
            if (t < tMin) tMin = t;
            else if (t > tMax) tMax = t;

            if (tMin > 1 + origin1 || tMax < origin1) return false;
        }

        return true;
    }
}