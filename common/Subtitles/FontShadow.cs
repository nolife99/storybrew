using System.Drawing;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font drop shadow effect. </summary>
    public class FontShadow : FontEffect
    {
        ///<summary> The thickness of the shadow. </summary>
        public readonly int Thickness;

        ///<summary> The color tinting of the shadow. </summary>
        public readonly FontColor Color;

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public SizeF Measure => new Size(Thickness * 2, Thickness * 2);

        ///<summary> Creates a new <see cref="FontShadow"/> descriptor with information about a drop shadow effect. </summary>
        ///<param name="thickness"> The thickness of the shadow. </param>
        ///<param name="color"> The color tinting of the shadow. </param>
        public FontShadow(int thickness = 1, FontColor color = default)
        {
            Thickness = thickness;
            Color = color;
        }

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            if (Thickness < 1) return;
            using (var brush = new SolidBrush(Color)) for (var i = 1; i <= Thickness; ++i) textGraphics.DrawString(text, font, brush, x + i, y + i, stringFormat);
        }
    }
}