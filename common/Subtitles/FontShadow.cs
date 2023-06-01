using System.Drawing;
using System.Numerics;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font drop shadow effect. </summary>
    public class FontShadow : FontEffect
    {
        ///<summary> The thickness of the shadow. </summary>
        public int Thickness = 1;

        ///<summary> The color tinting of the shadow. </summary>
        public FontColor Color = new FontColor(0, 0, 0, 100);

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public Vector2 Measure() => new Vector2(Thickness * 2);

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            if (Thickness < 1) return;
            using (var brush = new SolidBrush(Color)) for (var i = 1; i <= Thickness; ++i) textGraphics.DrawString(text, font, brush, x + i, y + i, stringFormat);
        }
    }
}