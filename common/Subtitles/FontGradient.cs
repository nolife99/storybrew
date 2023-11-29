using System.Drawing;
using System.Drawing.Drawing2D;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font gradient effect. </summary>
    ///<remarks> Creates a new <see cref="FontGradient"/> with a descriptor about a smooth gradient effect. </remarks>
    ///<param name="offset"> The gradient offset of the effect. </param>
    ///<param name="size"> The relative size of the gradient. </param>
    ///<param name="color"> The color tinting of the gradient. </param>
    ///<param name="wrapMode"> How the gradient is tiled when it is smaller than the area being filled. </param>
    public class FontGradient(PointF offset = default, SizeF size = default, FontColor color = default, WrapMode wrapMode = WrapMode.TileFlipXY) : FontEffect
    {
        ///<summary> The gradient offset of the effect. </summary>
        public readonly PointF Offset = offset;

        ///<summary> The relative size of the gradient. </summary>
        public readonly SizeF Size = size;

        ///<summary> The color tinting of the gradient. </summary>
        public readonly FontColor Color = color;

        ///<summary> How the gradient is tiled when it is smaller than the area being filled. </summary>
        public readonly WrapMode WrapMode = wrapMode;

        ///<inheritdoc/>
        public bool Overlay => true;

        ///<inheritdoc/>
        public SizeF Measure => default;

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            var transparentColor = FontColor.FromRgba(Color.R, Color.G, Color.B, 0);
            using LinearGradientBrush brush = new(new PointF(x + Offset.X, y + Offset.Y), new(x + Offset.X + Size.Width, y + Offset.Y + Size.Height), Color, transparentColor)
            { 
                WrapMode = WrapMode 
            };
            textGraphics.DrawString(text, font, brush, x, y, stringFormat);
        }
    }
}