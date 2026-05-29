namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Renderers;
using Shaders;
using Textures;

/// <summary>
///     Converts between the abstract graphics enums in <see cref="BrewLib.Graphics" /> and the wgpu-native enums.
///     Centralising these conversions in one place makes the rest of the backend much easier to read — every other
///     file can speak in domain types while this file owns the translation tables.
/// </summary>
static class WgpuMapper
{
    public static WGPUVertexFormat ToWgpu(VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 => WGPUVertexFormat.Float32,
            VertexAttributeFormat.Float32x2 => WGPUVertexFormat.Float32x2,
            VertexAttributeFormat.Float32x3 => WGPUVertexFormat.Float32x3,
            VertexAttributeFormat.Float32x4 => WGPUVertexFormat.Float32x4,
            // Float32Mat3x2 is laid out as three vec2 locations from the renderer's perspective, but the buffer
            // representation is a tightly packed 6-float matrix. We expose it as Float32x2 per location and the
            // caller is expected to allocate three contiguous shader locations for it.
            VertexAttributeFormat.Float32Mat3x2 => WGPUVertexFormat.Float32x2,
            VertexAttributeFormat.Float16x2 => WGPUVertexFormat.Float16x2,
            VertexAttributeFormat.Float16x4 => WGPUVertexFormat.Float16x4,
            VertexAttributeFormat.Unorm8x4 => WGPUVertexFormat.Unorm8x4,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported VertexAttributeFormat")
        };

    public static WGPUPrimitiveTopology ToWgpu(PrimitiveTopology topology)
        => topology switch
        {
            PrimitiveTopology.Points => WGPUPrimitiveTopology.PointList,
            PrimitiveTopology.Lines => WGPUPrimitiveTopology.LineList,
            PrimitiveTopology.Triangles => WGPUPrimitiveTopology.TriangleList,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, "Unsupported PrimitiveTopology")
        };

    public static WGPUAddressMode ToWgpu(TextureWrap wrap)
        => wrap switch
        {
            // WebGPU has no separate "clamp" vs "clamp-to-edge"; both map to ClampToEdge. Likewise it has no
            // ClampToBorder — best effort is ClampToEdge.
            TextureWrap.Clamp or TextureWrap.ClampToEdge or TextureWrap.ClampToBorder => WGPUAddressMode.ClampToEdge,
            TextureWrap.Repeat => WGPUAddressMode.Repeat,
            TextureWrap.MirroredRepeat => WGPUAddressMode.MirrorRepeat,
            _ => WGPUAddressMode.ClampToEdge
        };

    /// <summary>
    ///     Maps a <see cref="TextureFilter" /> to its WGPU min/mag filter plus the mipmap filter that should accompany it.
    /// </summary>
    public static (WGPUFilterMode MinMag, WGPUMipmapFilterMode Mipmap, bool WantsMips) ToWgpuFilter(TextureFilter filter)
        => filter switch
        {
            TextureFilter.Nearest => (WGPUFilterMode.Nearest, WGPUMipmapFilterMode.Nearest, false),
            TextureFilter.Linear => (WGPUFilterMode.Linear, WGPUMipmapFilterMode.Nearest, false),
            TextureFilter.NearestMipmapNearest => (WGPUFilterMode.Nearest, WGPUMipmapFilterMode.Nearest, true),
            TextureFilter.LinearMipmapNearest => (WGPUFilterMode.Linear, WGPUMipmapFilterMode.Nearest, true),
            TextureFilter.NearestMipmapLinear => (WGPUFilterMode.Nearest, WGPUMipmapFilterMode.Linear, true),
            TextureFilter.LinearMipmapLinear => (WGPUFilterMode.Linear, WGPUMipmapFilterMode.Linear, true),
            _ => (WGPUFilterMode.Linear, WGPUMipmapFilterMode.Nearest, false)
        };

    public static WGPUBlendFactor ToWgpu(BlendFactor factor)
        => factor switch
        {
            BlendFactor.Zero => WGPUBlendFactor.Zero,
            BlendFactor.One => WGPUBlendFactor.One,
            BlendFactor.SrcAlpha => WGPUBlendFactor.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => WGPUBlendFactor.OneMinusSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, "Unsupported BlendFactor")
        };

    /// <summary>
    ///     Builds a <see cref="WGPUBlendState" /> from a <see cref="BlendingFactorState" />. Disabled blending is
    ///     represented as Source=One, Destination=Zero (the identity blend) so callers can always emit a blend
    ///     state and let the shader output pass through unchanged when blending is "off".
    /// </summary>
    public static WGPUBlendState ToWgpu(BlendingFactorState state)
    {
        if (!state.Enabled)
            return new()
            {
                color = new()
                {
                    operation = WGPUBlendOperation.Add,
                    srcFactor = WGPUBlendFactor.One,
                    dstFactor = WGPUBlendFactor.Zero
                },
                alpha = new()
                {
                    operation = WGPUBlendOperation.Add,
                    srcFactor = WGPUBlendFactor.One,
                    dstFactor = WGPUBlendFactor.Zero
                }
            };

        return new()
        {
            color = new()
            {
                operation = WGPUBlendOperation.Add,
                srcFactor = ToWgpu(state.Source),
                dstFactor = ToWgpu(state.Destination)
            },
            alpha = new()
            {
                operation = WGPUBlendOperation.Add,
                srcFactor = ToWgpu(state.AlphaSource),
                dstFactor = ToWgpu(state.AlphaDestination)
            }
        };
    }

    /// <summary>
    ///     Translates the WGSL shader binding stage to a WGPU visibility flag set.
    /// </summary>
    public static ShaderStage ToWgpu(ShaderBindingStage stage)
        => stage switch
        {
            ShaderBindingStage.Vertex => ShaderStage.Vertex,
            ShaderBindingStage.Fragment => ShaderStage.Fragment,
            ShaderBindingStage.Compute => ShaderStage.Compute,
            _ => ShaderStage.None
        };

    /// <summary>
    ///     Round <paramref name="value" /> up to a multiple of <paramref name="alignment" />. Alignment must be a power of
    ///     two.
    /// </summary>
    public static int AlignUp(int value, int alignment)
    {
        var mask = alignment - 1;
        return value + mask & ~mask;
    }

    /// <summary>
    ///     Round <paramref name="value" /> up to a multiple of <paramref name="alignment" />. Alignment must be a power of
    ///     two.
    /// </summary>
    public static ulong AlignUp(ulong value, ulong alignment)
    {
        var mask = alignment - 1;
        return value + mask & ~mask;
    }
}