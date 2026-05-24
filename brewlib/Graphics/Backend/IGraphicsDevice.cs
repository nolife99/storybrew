namespace BrewLib.Graphics.Backend;

using System;
using Shaders;
using SixLabors.ImageSharp;
using Textures;

public interface IGraphicsDevice : IDisposable
{
    void InitializeTextureSlots(int textureSlotCount);
    void ResetStateCache();

    void SetViewport(Rectangle viewport);
    void SetScissor(Rectangle? region);
    void SetCapability(GraphicsCapability capability, bool enabled);
    void SetBlendState(BlendingFactorState state);

    void UseProgram(int programId);
    void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader);
    void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader);

    int BindTexture(ITexture texture);
    void BindTextures(scoped ReadOnlySpan<ITexture> textures, Span<int> textureUnits);
    void UnbindTexture(ITexture texture);
}

public enum GraphicsCapability
{
    Blend,
    ScissorTest,
    FramebufferSrgb
}