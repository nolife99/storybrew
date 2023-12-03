using System.Drawing;

namespace StorybrewCommon.Subtitles;

///<summary> A font outline effect. </summary>
///<remarks> Creates a new <see cref="FontOutline"/> descriptor with information about an outlining effect. </remarks>
///<param name="thickness"> The thickness of the outline. </param>
///<param name="color"> The color of the outline. </param>
public class FontOutline(int thickness = 1, FontColor color = default) : FontEffect
{
    const float diagonal = 1.41421356237f;

    ///<summary> The thickness of the outline. </summary>
    public int Thickness => thickness;

    ///<summary> The color of the outline. </summary>
    public FontColor Color => color;

    ///<inheritdoc/>
    public bool Overlay => false;

    ///<inheritdoc/>
    public SizeF Measure => new(Thickness * diagonal * 2, Thickness * diagonal * 2);

    ///<inheritdoc/>
    public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
    {
        if (Thickness < 1) return;

        using SolidBrush brush = new(Color);

        for (var i = 1; i <= Thickness; ++i)
        if ((i & 1) == 0)
        {
            textGraphics.DrawString(text, font, brush, x - i * diagonal, y, stringFormat);
            textGraphics.DrawString(text, font, brush, x, y - i * diagonal, stringFormat);
            textGraphics.DrawString(text, font, brush, x + i * diagonal, y, stringFormat);
            textGraphics.DrawString(text, font, brush, x, y + i * diagonal, stringFormat);
        }
        else
        {
            textGraphics.DrawString(text, font, brush, x - i, y - i, stringFormat);
            textGraphics.DrawString(text, font, brush, x - i, y + i, stringFormat);
            textGraphics.DrawString(text, font, brush, x + i, y + i, stringFormat);
            textGraphics.DrawString(text, font, brush, x + i, y - i, stringFormat);
        }
    }
}