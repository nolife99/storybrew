namespace BrewLib.Graphics.Backend.SDL;

using System;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using SDL3;
using SixLabors.ImageSharp;

public sealed class SdlGraphicsDevice : IGraphicsDevice
{
    readonly SdlGraphicsBackend backend;

    BlendingFactorState blendState = new(BlendingMode.AlphaBlend);
    Rectangle viewport;
    Rectangle? scissor;
    uint swapchainHeight;
    bool disposed;

    public SdlGraphicsDevice(SdlGraphicsBackend backend, nint deviceHandle)
    {
        this.backend = backend;
        DeviceHandle = deviceHandle;
    }

    public nint DeviceHandle { get; }
    public BlendingFactorState BlendState => blendState;

    public void InitializeTextureSlots(int textureSlotCount)
    {
    }

    public void ResetStateCache()
    {
    }

    public void SetViewport(Rectangle viewport)
    {
        this.viewport = viewport;
        swapchainHeight = backend?.SwapchainHeight ?? 0;
        if (backend?.RenderPass != nint.Zero) applyViewport(backend.RenderPass);
    }

    public void SetScissor(Rectangle? region)
    {
        scissor = region;
        if (backend?.RenderPass != nint.Zero) applyScissor(backend.RenderPass);
    }

    public void SetCapability(GraphicsCapability capability, bool enabled)
    {
    }

    public void SetBlendState(BlendingFactorState state)
    {
        blendState = state;
    }

    public void UseProgram(int programId)
    {
    }

    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader)
        => throw new NotSupportedException("SDL GPU does not use the legacy OpenGL vertex attribute path");

    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader)
        => throw new NotSupportedException("SDL GPU does not use the legacy OpenGL vertex attribute path");

    public int BindTexture(ITexture texture)
        => throw new NotSupportedException("SDL GPU textures are bound through resource sets, not global texture units");

    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, Span<int> textureUnits)
        => throw new NotSupportedException("SDL GPU textures are bound through resource sets, not global texture units");

    public void UnbindTexture(ITexture texture)
    {
    }

    public void Dispose()
    {
        if (disposed) return;

        SDL.WaitForGPUIdle(DeviceHandle);
        SDL.DestroyGPUDevice(DeviceHandle);

        disposed = true;
    }

    internal void ApplyRenderPassState(nint renderPass)
    {
        applyViewport(renderPass);
        applyScissor(renderPass);
    }

    void applyViewport(nint renderPass)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        // Convert from OpenGL coordinates (bottom-left origin) to SDL GPU coordinates (top-left origin)
        var y = swapchainHeight > 0 ? (int)swapchainHeight - viewport.Y - viewport.Height : viewport.Y;

        SDL.GPUViewport gpuViewport = new()
        {
            X = viewport.X,
            Y = y,
            W = viewport.Width,
            H = viewport.Height,
            MinDepth = 0,
            MaxDepth = 1
        };
        SDL.SetGPUViewport(renderPass, in gpuViewport);
    }

    void applyScissor(nint renderPass)
    {
        // SDL GPU scissor is always active (no enable/disable like OpenGL)
        // When scissor is null, use the full viewport to effectively disable it
        var region = scissor ?? viewport;
        if (region.Width <= 0 || region.Height <= 0) return;

        // Convert from OpenGL framebuffer coordinates (bottom-left origin) to SDL GPU coordinates (top-left origin)
        var y = swapchainHeight > 0 ? (int)swapchainHeight - region.Y - region.Height : region.Y;
        SDL.Rect rect = new()
        {
            X = region.X,
            Y = y,
            W = region.Width,
            H = region.Height
        };
        SDL.SetGPUScissor(renderPass, in rect);
    }
}
