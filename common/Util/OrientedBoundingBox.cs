namespace StorybrewCommon.Util;

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp;

#pragma warning disable CS1591
public readonly record struct OrientedBoundingBox : IDisposable
{
    readonly Vector2[] corners = ArrayPool<Vector2>.Shared.Rent(4), axis = ArrayPool<Vector2>.Shared.Rent(2);
    readonly float[] origins = ArrayPool<float>.Shared.Rent(4);
    public OrientedBoundingBox(Vector2 position, Vector2 origin, float width, float height, float angle)
    {
        var (sin, cos) = float.SinCos(angle);
        Vector2 unitRight = new(cos, sin), unitUp = new(-sin, cos);

        var right = unitRight * (width - origin.X);
        var up = unitUp * (height - origin.Y);
        var left = unitRight * -origin.X;
        var down = unitUp * -origin.Y;

        corners[0] = position + left + down;
        corners[1] = position + right + down;
        corners[2] = position + right + up;
        corners[3] = position + left + up;

        axis[0] = corners[1] - corners[0];
        axis[1] = corners[3] - corners[0];
        for (var a = 0; a < 2; ++a)
        {
            axis[a] /= axis[a].LengthSquared();
            origins[a] = Vector2.Dot(corners[0], axis[a]);
        }
    }
    public void Dispose()
    {
        ArrayPool<Vector2>.Shared.Return(corners);
        ArrayPool<Vector2>.Shared.Return(axis);
        ArrayPool<float>.Shared.Return(origins);
    }
    public RectangleF GetAABB()
    {
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var corner in corners)
        {
            minX = Math.Min(minX, corner.X);
            maxX = Math.Max(maxX, corner.X);
            minY = Math.Min(minY, corner.Y);
            maxY = Math.Max(maxY, corner.Y);
        }

        return RectangleF.FromLTRB(minX, minY, maxX, maxY);
    }
    bool Intersects(ref readonly OrientedBoundingBox other) => intersects1Way(in other) && other.intersects1Way(in this);
    public bool Intersects(ref readonly RectangleF other)
    {
        using OrientedBoundingBox otherBox = new(other.Location, Vector2.Zero, other.Width, other.Height, 0);
        return Intersects(in otherBox);
    }
    bool intersects1Way(ref readonly OrientedBoundingBox other)
    {
        for (var a = 0; a < 2; ++a)
        {
            var axis = this.axis[a];
            var t = Vector2.Dot(other.corners[0], axis);
            var tMin = t;
            var tMax = t;

            for (var c = 1; c < 4; ++c)
            {
                t = Vector2.Dot(other.corners[c], axis);
                if (t < tMin) tMin = t;
                else if (t > tMax) tMax = t;
            }

            var origin = origins[a];
            if (tMin > 1 + origin || tMax < origin) return false;
        }

        return true;
    }
}