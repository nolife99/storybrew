using System.Drawing;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font background effect. </summary>
    public class FontBackground : FontEffect
    {
        ///<summary> The coloring tint of the glow. </summary>
        public readonly FontColor Color;

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public SizeF Measure => default;

        ///<summary> Creates a new <see cref="FontBackground"/> descriptor with information about a font background. </summary>
        ///<param name="color"> The coloring tint of the glow. </param>
        public FontBackground(FontColor color = default) => Color = color;

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y) => textGraphics.Clear(Color);
    }
}