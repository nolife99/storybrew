namespace BrewLib.Graphics.Backend.WebGPU;

using Ahjo.Wgpu;

readonly struct WebGpuSurfaceFrame(Texture texture, TextureView textureView, bool shouldPresent)
{
    public readonly Texture Texture = texture;
    public readonly TextureView TextureView = textureView;
    public readonly bool ShouldPresent = shouldPresent;
}