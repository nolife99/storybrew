using osuTK;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace StorybrewCommon.Util
{
#pragma warning disable CS1591
    public static class Box2Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Box2 IntersectWith(this Box2 box2, Box2 other) => new(
            Math.Max(box2.Left, other.Left), Math.Max(box2.Top, other.Top),
            Math.Min(box2.Right, other.Right), Math.Min(box2.Bottom, other.Bottom));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Box2 IntersectWith(this Box2 box2, RectangleF other) => new(
            Math.Max(box2.Left, other.Left), Math.Max(box2.Top, other.Top),
            Math.Min(box2.Right, other.Right), Math.Min(box2.Bottom, other.Bottom));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RectangleF IntersectWith(this RectangleF box2, RectangleF other) => RectangleF.FromLTRB(
            Math.Max(box2.Left, other.Left), Math.Max(box2.Top, other.Top),
            Math.Min(box2.Right, other.Right), Math.Min(box2.Bottom, other.Bottom));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RectangleF IntersectWith(this RectangleF box2, Box2 other) => RectangleF.FromLTRB(
            Math.Max(box2.Left, other.Left), Math.Max(box2.Top, other.Top),
            Math.Min(box2.Right, other.Right), Math.Min(box2.Bottom, other.Bottom));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Area(this Box2 box) => box.Width * box.Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Area(this RectangleF box) => box.Width * box.Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Area(this Vector2 box) => box.X * box.Y;
    }
}