﻿namespace BrewLib.UserInterface.Skinning.Styles;

using SixLabors.ImageSharp.PixelFormats;
using Util;

public record ImageStyle : WidgetStyle
{
    public Rgba32 Color;
    public ScaleMode ScaleMode;
}