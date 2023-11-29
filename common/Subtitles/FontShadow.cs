using System.Drawing;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font drop shadow effect. </summary>
    ///<remarks> Creates a new <see cref="FontShadow"/> descriptor with information about a drop shadow effect. </remarks>
    ///<param name="thickness"> The thickness of the shadow. </param>
    ///<param name="color"> The color tinting of the shadow. </param>
    public class FontShadow(int thickness = 1, FontColor color = default) : FontEffect
    {
        ///<summary> The thickness of the shadow. </summary>
        public readonly int Thickness = thickness;

        ///<summary> The color tinting of the shadow. </summary>
        public readonly FontColor Color = color;

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public SizeF Measure => new(Thickness * 2, Thickness * 2);

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            if (Thickness < 1) return;

            using SolidBrush brush = new(Color); 
            for (var i = 1; i <= Thickness; ++i) textGraphics.DrawString(text, font, brush, x + i, y + i, stringFormat);
        }
    }
}