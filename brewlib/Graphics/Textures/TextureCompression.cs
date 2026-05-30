namespace BrewLib.Graphics.Textures;

using System;
using BasisBlockEncoder;

[Flags]
public enum TextureCompressionFormats
{
    None = 0,

    Bc1 = 1 << 0,
    Bc3 = 1 << 1,
    Bc4 = 1 << 2,
    Bc5 = 1 << 3,
    Bc6h = 1 << 4,
    Bc7 = 1 << 5,

    Etc2 = 1 << 6,
    Astc = 1 << 7,

    Bc = Bc1 | Bc3 | Bc4 | Bc5 | Bc6h | Bc7
}

internal readonly record struct BcPlan(
    BcFormat Format,
    int ContentX,
    int ContentY,
    int ContentW,
    int ContentH,
    bool HasAlpha);

internal readonly record struct BcCompressionPolicy(int MinArea, bool TrimTransparent);
