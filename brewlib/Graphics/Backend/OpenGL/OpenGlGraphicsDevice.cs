namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using osuTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic;

public sealed class OpenGlGraphicsDevice : IGraphicsDevice
{
    readonly PooledDictionary<EnableCap, bool> capabilityCache = new();

    int activeTextureUnit, lastRecycledTextureUnit = -1, programId;
    int[] samplerTextureIds = [];
    TextureTarget[] samplerTexturingModes = [];

    public int ActiveTextureUnit
    {
        get => activeTextureUnit;
        set
        {
            if (activeTextureUnit == value) return;

            GL.ActiveTexture(TextureUnit.Texture0 + value);
            activeTextureUnit = value;
        }
    }

    public void InitializeTextureSlots(int textureSlotCount)
    {
        samplerTextureIds = new int[textureSlotCount];
        samplerTexturingModes = new TextureTarget[textureSlotCount];
        lastRecycledTextureUnit = -1;
        activeTextureUnit = 0;
    }

    public void ResetStateCache() => capabilityCache.Clear();

    public void SetViewport(Rectangle viewport)
        => GL.Viewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);

    public void SetScissor(Rectangle? region)
    {
        SetCapability(GraphicsCapability.ScissorTest, region.HasValue);
        if (!region.HasValue) return;

        var value = region.Value;
        GL.Scissor(value.X, value.Y, value.Width, value.Height);
    }

    public void SetCapability(GraphicsCapability capability, bool enabled)
        => SetCapability(toOpenGlCapability(capability), enabled);

    public void SetBlendState(BlendingFactorState state)
    {
        SetCapability(GraphicsCapability.Blend, state.Enabled);
        if (!state.Enabled) return;

        GL.BlendFuncSeparate(toOpenGlBlendFactorSrc(state.Source),
            toOpenGlBlendFactorDest(state.Destination),
            toOpenGlBlendFactorSrc(state.AlphaSource),
            toOpenGlBlendFactorDest(state.AlphaDestination));
    }

    public void UseProgram(int programId)
    {
        if (this.programId == programId) return;

        this.programId = programId;
        GL.UseProgram(programId);
    }

    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader)
    {
        foreach (var attribute in declaration)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation < 0) continue;

            GL.EnableVertexAttribArray(attributeLocation);
            GL.VertexAttribPointer(attributeLocation,
                attribute.ComponentCount,
                toOpenGlVertexAttributeType(attribute.Format),
                attribute.Normalized,
                declaration.VertexSize,
                attribute.Offset);
        }
    }

    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader)
    {
        foreach (var attribute in declaration)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation >= 0) GL.DisableVertexAttribArray(attributeLocation);
        }
    }

    public int BindTexture(ITexture texture)
    {
        if (texture is not Texture2d texture2d)
            throw new InvalidOperationException($"{nameof(OpenGlGraphicsDevice)} can only bind OpenGL textures");

        return BindTexture(texture2d.TextureId);
    }

    public int BindTexture(int textureId) => BindTextures([textureId]);

    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, Span<int> textureUnits)
    {
        if (textureUnits.Length < textures.Length)
            throw new ArgumentException("The texture unit output span is too small", nameof(textureUnits));

        Span<int> textureIds = stackalloc int[textures.Length];
        for (var i = 0; i < textures.Length; ++i)
        {
            if (textures[i] is not Texture2d texture2d)
                throw new InvalidOperationException($"{nameof(OpenGlGraphicsDevice)} can only bind OpenGL textures");

            textureIds[i] = texture2d.TextureId;
        }

        BindTextures(textureIds, textureUnits[..textures.Length]);
    }

    public void BindPrimaryTexture(int textureId, TextureTarget mode = TextureTarget.Texture2D)
        => BindTexture(textureId, 0, mode);

    public void UnbindTexture(ITexture texture)
    {
        if (texture is Texture2d texture2d) UnbindTexture(texture2d.TextureId);
    }

    public void UnbindTexture(int textureId)
    {
        var i = Array.IndexOf(samplerTextureIds, textureId, 0, samplerTextureIds.Length);
        if (i == -1) return;

        samplerTextureIds[i] = 0;

        ActiveTextureUnit = i;
        GL.BindTexture(samplerTexturingModes[i], 0);
    }

    public void Dispose() => capabilityCache.Dispose();

    void BindTexture(int textureId, int samplerIndex, TextureTarget mode)
    {
        ActiveTextureUnit = samplerIndex;
        SetTexturingMode(samplerIndex, mode);

        ref var samplerTextureId = ref samplerTextureIds[samplerIndex];
        if (samplerTextureId == textureId) return;

        GL.BindTexture(mode, textureId);
        samplerTextureId = textureId;
    }

    void SetTexturingMode(int samplerIndex, TextureTarget mode)
    {
        ref var previousMode = ref samplerTexturingModes[samplerIndex];
        if (previousMode == mode) return;

        if (samplerTextureIds[samplerIndex] != 0) UnbindTexture(samplerTextureIds[samplerIndex]);
        previousMode = mode;
    }

    int BindTextures(scoped ReadOnlySpan<int> textures)
    {
        Span<int> samplerIndexes = stackalloc int[textures.Length];
        BindTextures(textures, samplerIndexes);
        return samplerIndexes[0];
    }

    void BindTextures(scoped ReadOnlySpan<int> textures, Span<int> samplerIndexes)
    {
        for (var i = 0; i < textures.Length; ++i)
        {
            var textureId = textures[i];

            samplerIndexes[i] = -1;
            for (var j = 0; j < samplerTextureIds.Length; ++j)
                if (samplerTextureIds[j] == textureId)
                {
                    samplerIndexes[i] = j;
                    break;
                }
        }

        var samplerCount = samplerTextureIds.Length;
        for (var i = 0; i < textures.Length; ++i)
        {
            if (samplerIndexes[i] != -1) continue;

            var first = true;
            var samplerStartIndex = (lastRecycledTextureUnit + 1) % samplerCount;
            for (var samplerIndex = samplerStartIndex; first || samplerIndex != samplerStartIndex;
                samplerIndex = (samplerIndex + 1) % samplerCount)
            {
                first = false;

                var isFreeSamplerUnit = true;
                foreach (var usedIndex in samplerIndexes)
                {
                    if (usedIndex != samplerIndex) continue;

                    isFreeSamplerUnit = false;
                    break;
                }

                if (!isFreeSamplerUnit) continue;

                BindTexture(textures[i], samplerIndex, TextureTarget.Texture2D);
                samplerIndexes[i] = samplerIndex;
                lastRecycledTextureUnit = samplerIndex;
                break;
            }
        }
    }

    void SetCapability(EnableCap capability, bool enable)
    {
        if (capabilityCache.TryGetValue(capability, out var isEnabled) && isEnabled == enable) return;

        if (enable) GL.Enable(capability);
        else GL.Disable(capability);

        capabilityCache[capability] = enable;
    }

    static EnableCap toOpenGlCapability(GraphicsCapability capability)
        => capability switch
        {
            GraphicsCapability.Blend => EnableCap.Blend,
            GraphicsCapability.ScissorTest => EnableCap.ScissorTest,
            GraphicsCapability.FramebufferSrgb => EnableCap.FramebufferSrgb,
            _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, null)
        };

    static VertexAttribPointerType toOpenGlVertexAttributeType(VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 or
                VertexAttributeFormat.Float32x2 or
                VertexAttributeFormat.Float32x3 or
                VertexAttributeFormat.Float32x4 => VertexAttribPointerType.Float,
            VertexAttributeFormat.Float16x2 or VertexAttributeFormat.Float16x4 => VertexAttribPointerType.HalfFloat,
            VertexAttributeFormat.Unorm8x4 => VertexAttribPointerType.UnsignedByte,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    static BlendingFactorSrc toOpenGlBlendFactorSrc(BlendFactor factor)
        => factor switch
        {
            BlendFactor.Zero => BlendingFactorSrc.Zero,
            BlendFactor.One => BlendingFactorSrc.One,
            BlendFactor.SrcAlpha => BlendingFactorSrc.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => BlendingFactorSrc.OneMinusSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
        };

    static BlendingFactorDest toOpenGlBlendFactorDest(BlendFactor factor)
        => factor switch
        {
            BlendFactor.Zero => BlendingFactorDest.Zero,
            BlendFactor.One => BlendingFactorDest.One,
            BlendFactor.SrcAlpha => BlendingFactorDest.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => BlendingFactorDest.OneMinusSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
        };
}
