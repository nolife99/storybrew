namespace BrewLib.Util;

public readonly record struct FourSide(float Top, float Right, float Bottom, float Left)
{
    public FourSide(float all) : this(all, all, all, all) { }
    public FourSide(float vertical, float horizontal) : this(vertical, horizontal, vertical, horizontal) { }
    public FourSide(float top, float horizontal, float bottom) : this(top, horizontal, bottom, horizontal) { }
    public float Horizontal => Left + Right;
    public float Vertical => Top + Bottom;

    public float GetHorizontalOffset(BoxAlignment alignment) => (alignment & BoxAlignment.Left) > 0 ? Left :
        (alignment & BoxAlignment.Right) > 0 ? -Right : 0;

    public float GetVerticalOffset(BoxAlignment alignment) => (alignment & BoxAlignment.Top) > 0 ? Top :
        (alignment & BoxAlignment.Bottom) > 0 ? -Bottom : 0;
}