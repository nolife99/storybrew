using System.Drawing;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font outline effect. </summary>
    public class FontOutline : FontEffect
    {
        const float diagonal = 1.41421356237f;

        ///<summary> The thickness of the outline. </summary>
        public readonly int Thickness;

        ///<summary> The color of the outline. </summary>
        public readonly FontColor Color;

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public SizeF Measure => new SizeF(Thickness * diagonal * 2, Thickness * diagonal * 2);

        ///<summary> Creates a new <see cref="FontOutline"/> descriptor with information about an outlining effect. </summary>
        ///<param name="thickness"> The thickness of the outline. </param>
        ///<param name="color"> The color of the outline. </param>
        public FontOutline(int thickness = 1, FontColor color = default)
        {
            Thickness = thickness;
            Color = color;
        }

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            if (Thickness < 1) return;

            using (var brush = new SolidBrush(Color)) for (var i = 1; i <= Thickness; ++i)
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
}