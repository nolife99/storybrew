namespace BrewLib.Graphics.Renderers;

using System;
using Textures;

public sealed class TextureSlotter
{
    readonly ITexture[] textures;

    public TextureSlotter(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, null);

        textures = new ITexture[capacity];
    }

    public int Capacity => textures.Length;
    public int Count { get; private set; }

    public ReadOnlySpan<ITexture> Textures => textures.AsSpan(0, Count);

    public static TextureSlotter ForFragmentShader()
        => new(int.Max(1, DrawState.MaxTextureImageUnits));

    public bool TryGetSlot(ITexture texture, out int slot)
    {
        for (var i = 0; i < Count; ++i)
            if (ReferenceEquals(textures[i], texture))
            {
                slot = i;
                return true;
            }

        if (Count == textures.Length)
        {
            slot = -1;
            return false;
        }

        slot = Count;
        textures[Count++] = texture;
        return true;
    }

    public void Clear()
    {
        Array.Clear(textures, 0, Count);
        Count = 0;
    }
}