using System.Drawing;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font background effect. </summary>
    ///<remarks> Creates a new <see cref="FontBackground"/> descriptor with information about a font background. </remarks>
    ///<param name="color"> The coloring tint of the glow. </param>
    public class FontBackground(FontColor color = default) : FontEffect
    {
        ///<summary> The coloring tint of the glow. </summary>
        public readonly FontColor Color = color;

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public SizeF Measure => default;

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y) => textGraphics.Clear(Color);
    }
}