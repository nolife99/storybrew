namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Shaders;
using SixLabors.ImageSharp;
using Textures;

sealed class WebGpuGraphicsDevice(WebGpuGraphicsBackend backend) : IGraphicsDevice
{
    Rectangle? scissor;
    Rectangle viewport;

    public BlendingFactorState BlendState { get; private set; } = new(BlendingMode.AlphaBlend);

    internal uint RenderPassStateSerial { get; private set; }

    public void InitializeTextureSlots(int textureSlotCount) { }

    public void ResetStateCache()
        => ++RenderPassStateSerial;

    public void SetViewport(Rectangle viewport)
    {
        this.viewport = viewport;
        ++RenderPassStateSerial;

        if (viewport.Width > 0 && viewport.Height > 0)
            backend.Resize((uint)viewport.Width, (uint)viewport.Height);
    }

    public void SetScissor(Rectangle? region)
    {
        scissor = region;
        ++RenderPassStateSerial;
    }

    public void SetCapability(GraphicsCapability capability, bool enabled) { }
    public void SetBlendState(BlendingFactorState state) => BlendState = state;
    public void UseProgram(int programId) { }
    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader) => throw prototype();
    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader) => throw prototype();
    public int BindTexture(ITexture texture) => throw prototype();
    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, Span<int> textureUnits) => throw prototype();
    public void UnbindTexture(ITexture texture) { }
    public void Dispose() { }

    internal void ApplyRenderPassState()
    {
        applyViewport();
        applyScissor();
    }

    void applyViewport()
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        var y = toNativeY(viewport);
        backend.SetViewport(viewport.X,
            y,
            viewport.Width,
            viewport.Height);
    }

    void applyScissor()
    {
        var region = scissor ?? viewport;
        if (!tryGetNativeScissor(region, out var x, out var y, out var width, out var height))
        {
            if (scissor.HasValue)
                backend.SetScissorRect(0, 0, 0, 0);

            return;
        }

        backend.SetScissorRect((uint)x,
            (uint)y,
            (uint)width,
            (uint)height);
    }

    int toNativeY(Rectangle region)
    {
        var framebufferHeight = backend.FramebufferHeight;
        var height = framebufferHeight != 0 && framebufferHeight <= int.MaxValue
            ? (int)framebufferHeight
            : viewport.Height;

        return height - region.Y - region.Height;
    }

    bool tryGetNativeScissor(Rectangle region, out int x, out int y, out int width, out int height)
    {
        var framebufferWidth = backend.FramebufferWidth;
        var framebufferHeight = backend.FramebufferHeight;
        var boundsWidth = framebufferWidth != 0 && framebufferWidth <= int.MaxValue
            ? (int)framebufferWidth
            : viewport.Width;

        var boundsHeight = framebufferHeight != 0 && framebufferHeight <= int.MaxValue
            ? (int)framebufferHeight
            : viewport.Height;

        var left = region.X;
        var top = boundsHeight - region.Y - region.Height;
        var right = left + region.Width;
        var bottom = top + region.Height;

        left = Math.Clamp(left, 0, boundsWidth);
        right = Math.Clamp(right, 0, boundsWidth);
        top = Math.Clamp(top, 0, boundsHeight);
        bottom = Math.Clamp(bottom, 0, boundsHeight);

        x = left;
        y = top;
        width = right - left;
        height = bottom - top;
        return width > 0 && height > 0;
    }

    static NotImplementedException prototype()
        => new("WebGPU prototype does not use the legacy graphics device path.");
}