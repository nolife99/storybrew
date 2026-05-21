namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Shaders;
using Silk.NET.WebGPU;
using SixLabors.ImageSharp;
using Textures;

sealed unsafe class WebGpuGraphicsDevice(WebGpuGraphicsBackend backend) : IGraphicsDevice
{
    BlendingFactorState blendState = new(BlendingMode.AlphaBlend);
    Rectangle viewport;
    Rectangle? scissor;

    public BlendingFactorState BlendState => blendState;

    public void InitializeTextureSlots(int textureSlotCount) { }
    public void ResetStateCache() { }

    public void SetViewport(Rectangle viewport)
    {
        this.viewport = viewport;
        if (viewport.Width > 0 && viewport.Height > 0)
            backend.Resize((uint)viewport.Width, (uint)viewport.Height);

        if (backend.RenderPass is not null)
            applyViewport(backend.RenderPass);
    }

    public void SetScissor(Rectangle? region)
    {
        scissor = region;
        if (backend.RenderPass is not null)
            applyScissor(backend.RenderPass);
    }

    public void SetCapability(GraphicsCapability capability, bool enabled) { }
    public void SetBlendState(BlendingFactorState state) => blendState = state;
    public void UseProgram(int programId) { }
    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader) => throw prototype();
    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader) => throw prototype();
    public int BindTexture(ITexture texture) => throw prototype();
    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, Span<int> textureUnits) => throw prototype();
    public void UnbindTexture(ITexture texture) { }
    public void Dispose() { }

    internal void ApplyRenderPassState(RenderPassEncoder* renderPass)
    {
        applyViewport(renderPass);
        applyScissor(renderPass);
    }

    void applyViewport(RenderPassEncoder* renderPass)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        var y = toNativeY(viewport);
        backend.Api.RenderPassEncoderSetViewport(renderPass,
            viewport.X,
            y,
            viewport.Width,
            viewport.Height,
            0,
            1);
    }

    void applyScissor(RenderPassEncoder* renderPass)
    {
        var region = scissor ?? viewport;
        if (!tryGetNativeScissor(region, out var x, out var y, out var width, out var height))
        {
            if (scissor.HasValue)
                backend.Api.RenderPassEncoderSetScissorRect(renderPass, 0, 0, 0, 0);
            return;
        }

        backend.Api.RenderPassEncoderSetScissorRect(renderPass,
            (uint)x,
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
