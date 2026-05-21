namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using Shaders;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using Textures;
using Tiny.PooledCollections.Generic;
using Shader = Shaders.Shader;

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

            OpenGlApi.GL.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + value));
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
        => OpenGlApi.GL.Viewport(viewport.X, viewport.Y, (uint)viewport.Width, (uint)viewport.Height);

    public void SetScissor(Rectangle? region)
    {
        SetCapability(GraphicsCapability.ScissorTest, region.HasValue);
        if (!region.HasValue) return;

        var value = region.Value;
        OpenGlApi.GL.Scissor(value.X, value.Y, (uint)value.Width, (uint)value.Height);
    }

    public void SetCapability(GraphicsCapability capability, bool enabled)
        => SetCapability(toOpenGlCapability(capability), enabled);

    public void SetBlendState(BlendingFactorState state)
    {
        SetCapability(GraphicsCapability.Blend, state.Enabled);
        if (!state.Enabled) return;

        OpenGlApi.GL.BlendFuncSeparate(toOpenGlBlendFactor(state.Source),
            toOpenGlBlendFactor(state.Destination),
            toOpenGlBlendFactor(state.AlphaSource),
            toOpenGlBlendFactor(state.AlphaDestination));
    }

    public void UseProgram(int programId)
    {
        if (this.programId == programId) return;

        this.programId = programId;
        OpenGlApi.GL.UseProgram((uint)programId);
    }

    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader)
    {
        foreach (var attribute in declaration)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation < 0) continue;

            OpenGlApi.GL.EnableVertexAttribArray((uint)attributeLocation);
            OpenGlApi.GL.VertexAttribPointer((uint)attributeLocation,
                attribute.ComponentCount,
                toOpenGlVertexAttributeType(attribute.Format),
                attribute.Normalized,
                (uint)declaration.VertexSize,
                attribute.Offset);
        }
    }

    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader)
    {
        foreach (var attribute in declaration)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation >= 0) OpenGlApi.GL.DisableVertexAttribArray((uint)attributeLocation);
        }
    }

    public int BindTexture(ITexture texture)
    {
        if (texture is not OpenGlTexture openGlTexture)
            throw new InvalidOperationException($"{nameof(OpenGlGraphicsDevice)} can only bind OpenGL textures");

        return BindTexture(openGlTexture.TextureId);
    }

    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, scoped Span<int> textureUnits)
    {
        if (textureUnits.Length < textures.Length)
            throw new ArgumentException("The texture unit output span is too small", nameof(textureUnits));

        Span<int> textureIds = stackalloc int[textures.Length];
        for (var i = 0; i < textures.Length; ++i)
        {
            if (textures[i] is not OpenGlTexture openGlTexture)
                throw new InvalidOperationException($"{nameof(OpenGlGraphicsDevice)} can only bind OpenGL textures");

            textureIds[i] = openGlTexture.TextureId;
        }

        BindTextures(textureIds, textureUnits[..textures.Length]);
    }

    public void UnbindTexture(ITexture texture)
    {
        if (texture is OpenGlTexture openGlTexture) UnbindTexture(openGlTexture.TextureId);
    }

    public void Dispose() => capabilityCache.Dispose();

    public int BindTexture(int textureId) => BindTextures([textureId]);

    public void BindPrimaryTexture(int textureId, TextureTarget mode = TextureTarget.Texture2D)
        => BindTexture(textureId, 0, mode);

    public void UnbindTexture(int textureId)
    {
        var i = Array.IndexOf(samplerTextureIds, textureId, 0, samplerTextureIds.Length);
        if (i == -1) return;

        samplerTextureIds[i] = 0;

        ActiveTextureUnit = i;
        OpenGlApi.GL.BindTexture(samplerTexturingModes[i], 0);
    }

    void BindTexture(int textureId, int samplerIndex, TextureTarget mode)
    {
        ActiveTextureUnit = samplerIndex;
        SetTexturingMode(samplerIndex, mode);

        ref var samplerTextureId = ref samplerTextureIds[samplerIndex];
        if (samplerTextureId == textureId) return;

        OpenGlApi.GL.BindTexture(mode, (uint)textureId);
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

    void BindTextures(scoped ReadOnlySpan<int> textures, scoped Span<int> samplerIndexes)
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

        if (enable) OpenGlApi.GL.Enable(capability);
        else OpenGlApi.GL.Disable(capability);

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

    static BlendingFactor toOpenGlBlendFactor(BlendFactor factor)
        => factor switch
        {
            BlendFactor.Zero => BlendingFactor.Zero,
            BlendFactor.One => BlendingFactor.One,
            BlendFactor.SrcAlpha => BlendingFactor.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => BlendingFactor.OneMinusSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
        };
}