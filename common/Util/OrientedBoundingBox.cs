using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Numerics;
using System.Drawing;

namespace StorybrewCommon.Util
{
#pragma warning disable CS1591
    public class OrientedBoundingBox
    {
        readonly CommandPosition[] corners = new CommandPosition[4], axis = new CommandPosition[2];
        readonly double[] origins = new double[2];

        public OrientedBoundingBox(CommandPosition position, CommandPosition origin, double width, double height, double angle)
        {
            var unitRight = new CommandPosition(Math.Cos(angle), Math.Sin(angle));
            var unitUp = new CommandPosition(-Math.Sin(angle), Math.Cos(angle));
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
                axis[a] /= axis[a].LengthSquared;
                origins[a] = Vector2.Dot(corners[0], axis[a]);
            }
        }
        public osuTK.Box2 GetAABBBox()
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var corner in corners)
            {
                minX = Math.Min(minX, corner.X);
                maxX = Math.Max(maxX, corner.X);
                minY = Math.Min(minY, corner.Y);
                maxY = Math.Max(maxY, corner.Y);
            }
            return new osuTK.Box2(minX, minY, maxX, maxY);
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

        public bool Intersects(OrientedBoundingBox other) => intersects1Way(other) && other.intersects1Way(this);
        public bool Intersects(RectangleF other) => Intersects(new OrientedBoundingBox(new Vector2(other.Left, other.Top), Vector2.Zero, other.Width, other.Height, 0));
        public bool Intersects(osuTK.Box2 other) => Intersects(new OrientedBoundingBox(new Vector2(other.Left, other.Top), Vector2.Zero, other.Width, other.Height, 0));
        bool intersects1Way(OrientedBoundingBox other)
        {
            for (var a = 0; a < 2; ++a)
            {
                var t = Vector2.Dot(other.corners[0], axis[a]);
                var tMin = t;
                var tMax = t;

                for (var c = 1; c < 4; ++c)
                {
                    t = Vector2.Dot(other.corners[c], axis[a]);
                    if (t < tMin) tMin = t;
                    else if (t > tMax) tMax = t;
                }
                if ((tMin > 1 + origins[a]) || (tMax < origins[a])) return false;
            }
            return true;
        }
    }
}