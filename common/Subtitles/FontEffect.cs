﻿using System.Drawing;
using System.Drawing.Drawing2D;

namespace StorybrewCommon.Subtitles;

#pragma warning disable CS1591
public interface FontEffect
{
    ///<summary> Whether to overlay the effect over the original texture. </summary>
    bool Overlay { get; }

    ///<summary> The vector radius of the font effect. </summary>
    SizeF Measure { get; }

    ///<summary> Draws the font effect over the texture. </summary>
    void Draw(Bitmap bitmap, Graphics textGraphics, GraphicsPath path, float x, float y);
}