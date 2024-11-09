namespace BrewLib.Util;

using System;

public readonly struct FourSide(float top, float right, float bottom, float left)
{
    public static readonly FourSide Zero = new(0);

    public float Top => top;
    public float Right => right;
    public float Bottom => bottom;
    public float Left => left;
    public float Horizontal => left + right;
    public float Vertical => top + bottom;

    public FourSide(float all) : this(all, all, all, all) { }
    public FourSide(float vertical, float horizontal) : this(vertical, horizontal, vertical, horizontal) { }
    public FourSide(float top, float horizontal, float bottom) : this(top, horizontal, bottom, horizontal) { }

    public static bool operator !=(FourSide left, FourSide right) => !(left == right);
    public static bool operator ==(FourSide left, FourSide right) => left.Top == right.Top && left.Right == right.Right &&
        left.Bottom == right.Bottom && left.Left == right.Left;

    public float GetHorizontalOffset(BoxAlignment alignment) => (alignment & BoxAlignment.Left) > 0 ? left :
        (alignment & BoxAlignment.Right) > 0 ? -right : 0;

    public float GetVerticalOffset(BoxAlignment alignment) => (alignment & BoxAlignment.Top) > 0 ? top :
        (alignment & BoxAlignment.Bottom) > 0 ? -bottom : 0;

    public override bool Equals(object obj) => this == (FourSide)obj;
    public override int GetHashCode() => HashCode.Combine(top, right, bottom, left);
    public override string ToString() => $"{top}, {right}, {bottom}, {left}";
}