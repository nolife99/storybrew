namespace BrewLib.Graphics.Renderers;

using System;
using Textures;

public sealed class TextureSlotter
{
    readonly ITexture[] textures;

    int count;

    public TextureSlotter(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, null);

        textures = new ITexture[capacity];
    }

    public int Capacity => textures.Length;
    public int Count => count;
    public ReadOnlySpan<ITexture> Textures => textures.AsSpan(0, count);

    public static TextureSlotter ForFragmentShader()
        => new(int.Max(1, DrawState.MaxTextureImageUnits));

    public bool TryGetSlot(ITexture texture, out int slot)
    {
        for (var i = 0; i < count; ++i)
            if (ReferenceEquals(textures[i], texture))
            {
                slot = i;
                return true;
            }

        if (count == textures.Length)
        {
            slot = -1;
            return false;
        }

        slot = count;
        textures[count++] = texture;
        return true;
    }

    public void Clear()
    {
        Array.Clear(textures, 0, count);
        count = 0;
    }
}
