namespace BrewLib.Util;

using System;

public readonly struct FourSide(float top, float right, float bottom, float left)
{
    public static readonly FourSide Zero = new(0);

    public readonly float Top = top, Right = right, Bottom = bottom, Left = left;
    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public FourSide(float all) : this(all, all, all, all) { }
    public FourSide(float vertical, float horizontal) : this(vertical, horizontal, vertical, horizontal) { }
    public FourSide(float top, float horizontal, float bottom) : this(top, horizontal, bottom, horizontal) { }

    public static bool operator ==(FourSide left, FourSide right)
        => left.Top == right.Top && left.Right == right.Right && left.Bottom == right.Bottom && left.Left == right.Left;

    public static bool operator !=(FourSide left, FourSide right) => !(left == right);

    public float GetHorizontalOffset(BoxAlignment alignment)
        => (alignment & BoxAlignment.Left) > 0 ? Left :
            (alignment & BoxAlignment.Right) > 0 ? -Right : 0;

    public float GetVerticalOffset(BoxAlignment alignment)
        => (alignment & BoxAlignment.Top) > 0 ? Top :
            (alignment & BoxAlignment.Bottom) > 0 ? -Bottom : 0;

    public override bool Equals(object obj) => this == (FourSide)obj;
    public override int GetHashCode() => HashCode.Combine(Top, Right, Bottom, Left);

    public override string ToString() => $"{Top}, {Right}, {Bottom}, {Left}";
}