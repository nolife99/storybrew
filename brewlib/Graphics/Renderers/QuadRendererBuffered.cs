namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public sealed class QuadRendererBuffered : IQuadRenderer
{
    const int IndexPerQuad = 6, VertexPerQuad = 4;

    const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture",
        ClipUniformName = "u_clipRect", StorageBufferOffsetUniformName = "u_ssboOffset";

    static readonly bool supportsRegionalBarrier =
            DrawState.SupportsImmutable && DrawState.Extensions.Contains("GL_ARB_ES3_1_compatibility"),
        supportsBarrier = supportsRegionalBarrier ||
            DrawState.SupportsImmutable && DrawState.Extensions.Contains("GL_ARB_shader_image_load_store");

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(false),
        VertexAttribute.CreateDiffuseCoord(true),
        VertexAttribute.CreateColor(true));

    readonly bool ownsShader;

    readonly IPrimitiveStreamer<QuadPrimitive> primitiveStreamer;
    readonly Shader shader;
    readonly int ssbo, ssboSize, maxQuadsPerBatch, textureUniformLocation, storageBufferOffsetUniformLocation;
    readonly nint ssboMap;

    ValueList<long> bindlessTextures;

    ICamera camera;
    ValueList<Vector4> clipRegions = ValueList.Create<Vector4>();
    ValueList<Matrix4x4> combinedMatrices = ValueList.Create<Matrix4x4>();
    int currentTexture, currentSamplerUnit = -1, ssboOffset;
    long currentTextureHandle;

    bool disposed, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 4096, int primitiveBufferSize = 0)
    {
        this.maxQuadsPerBatch = maxQuadsPerBatch;
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        ssboSize = Unsafe.SizeOf<Matrix4x4>() + Unsafe.SizeOf<Vector4>();
        if (Texture2d.BindlessTexturesSupported)
        {
            ssboSize += sizeof(long);
            bindlessTextures = ValueList.Create<long>();
        }
        else textureUniformLocation = shader.GetUniformLocation(TextureUniformName);

        storageBufferOffsetUniformLocation = shader.GetUniformLocation(StorageBufferOffsetUniformName);

        ssboSize *= maxQuadsPerBatch;

        var indicesCount = maxQuadsPerBatch * IndexPerQuad;
        using (var indicesBuffer = MemoryAllocator.Default.Allocate<ushort>(indicesCount))
        {
            var indices = indicesBuffer.Memory.Span;
            for (var i = 0; i < maxQuadsPerBatch; ++i)
            {
                var triangleIndex = i * IndexPerQuad;
                var quadIndex = i * VertexPerQuad;

                indices[triangleIndex] = indices[triangleIndex + 5] = (ushort)quadIndex;
                indices[triangleIndex + 1] = (ushort)(quadIndex + 1);
                indices[triangleIndex + 2] = indices[triangleIndex + 3] = (ushort)(quadIndex + 2);
                indices[triangleIndex + 4] = (ushort)(quadIndex + 3);
            }

            primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<QuadPrimitive>(VertexDeclaration,
                int.Max(maxQuadsPerBatch, primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize)),
                indices);
        }

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo = GL.GenBuffer());
        if (DrawState.SupportsImmutable)
        {
            var flags = supportsBarrier ?
                BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit :
                BufferStorageFlags.DynamicStorageBit;

            GL.BufferStorage(BufferTarget.ShaderStorageBuffer, ssboSize, 0, flags);

            if (supportsBarrier)
                ssboMap = GL.MapBufferRange(BufferTarget.ShaderStorageBuffer,
                    0,
                    ssboSize,
                    MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapPersistentBit |
                    MapBufferAccessMask.MapFlushExplicitBit | MapBufferAccessMask.MapInvalidateBufferBit |
                    MapBufferAccessMask.MapUnsynchronizedBit);
        }
        else GL.BufferData(BufferTarget.ShaderStorageBuffer, ssboSize, 0, BufferUsageHint.DynamicDraw);

        SDL.LogInfo(SDL.LogCategory.Render,
            $"Initialized {nameof(QuadRendererBuffered)} using {primitiveStreamer.GetType().Name}");
    }

    public Matrix4x4 TransformMatrix
    {
        get => transformMatrix;
        set
        {
            if (transformMatrix == value) return;

            DrawState.FlushRenderer();
            transformMatrix = value;
        }
    }

    public ICamera Camera
    {
        get => camera;
        set
        {
            if (camera == value) return;

            if (rendering) DrawState.FlushRenderer();
            camera = value;
        }
    }

    void IRenderer.BeginRendering()
    {
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo);

        shader.Begin();
        primitiveStreamer.Bind(shader);

        rendering = true;
    }

    void IRenderer.EndRendering()
    {
        primitiveStreamer.Unbind();
        shader.End();

        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0);

        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (primitiveStreamer.PrimitivesInBatch != 0)
        {
            combinedMatrices.Add(transformMatrix * camera.ProjectionView);
            if (Texture2d.BindlessTexturesSupported) bindlessTextures.Add(currentTextureHandle);

            var clipRegion = Rectangle.Intersect(DrawState.ClipRegion ?? Rectangle.Empty, DrawState.Viewport);
            if (clipRegion == Rectangle.Empty) clipRegion = DrawState.Viewport;

            clipRegions.Add(clipRegion);

            primitiveStreamer.QueueRender(IndexPerQuad, VertexPerQuad);
        }

        var queuedRenders = primitiveStreamer.QueuedRenders;
        if (!canBuffer || queuedRenders == 0) return;

        if (ssboOffset + queuedRenders > maxQuadsPerBatch) ssboOffset = 0;

        if (supportsBarrier) BufferSSBO();
        else BufferSSBOCompat();

        if (!Texture2d.BindlessTexturesSupported)
        {
            var samplerUnit = DrawState.BindTexture(currentTexture);
            if (currentSamplerUnit != samplerUnit)
            {
                GL.Uniform1(textureUniformLocation, samplerUnit);
                currentSamplerUnit = samplerUnit;
            }
        }

        GL.Uniform1(storageBufferOffsetUniformLocation, ssboOffset);

        ssboOffset += queuedRenders;

        if (Texture2d.BindlessTexturesSupported) bindlessTextures.Clear();
        combinedMatrices.Clear();
        clipRegions.Clear();

        primitiveStreamer.Render(PrimitiveType.Triangles, VertexPerQuad);
    }

    void IQuadRenderer.Draw(scoped ref readonly QuadPrimitive quad, Texture2dRegion texture)
    {
        if (Texture2d.BindlessTexturesSupported)
        {
            var textureId = texture.BindableTexture.BindlessTextureHandle;
            if (currentTextureHandle != textureId)
            {
                DrawState.FlushRenderer();
                currentTextureHandle = textureId;
            }
        }
        else
        {
            var textureId = texture.BindableTexture.TextureId;
            if (currentTexture != textureId)
            {
                DrawState.FlushRenderer(true);
                currentTexture = textureId;
            }
        }

        primitiveStreamer.AddPrimitive(in quad);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void BufferSSBO()
    {
        var span = MemoryMarshal.AsBytes(clipRegions.AsReadOnlySpan());
        var writeOffset = ssboOffset * Unsafe.SizeOf<Vector4>();

        var ssboSpan = ssboMap.AsSpan<byte>(ssboSize);
        primitiveStreamer.FrameSync.WaitAndLockRange(ssbo, writeOffset, span.Length);

        span.CopyTo(ssboSpan[writeOffset..]);
        GL.FlushMappedBufferRange(BufferTarget.ShaderStorageBuffer, writeOffset, span.Length);

        var section = maxQuadsPerBatch * Unsafe.SizeOf<Vector4>();

        if (Texture2d.BindlessTexturesSupported)
        {
            span = MemoryMarshal.AsBytes(bindlessTextures.AsReadOnlySpan());
            writeOffset = ssboOffset * sizeof(long);

            primitiveStreamer.FrameSync.WaitAndLockRange(ssbo, section + writeOffset, span.Length);

            span.CopyTo(ssboSpan[(section + writeOffset)..]);
            GL.FlushMappedBufferRange(BufferTarget.ShaderStorageBuffer, section + writeOffset, span.Length);

            section += maxQuadsPerBatch * sizeof(long);
        }

        span = MemoryMarshal.AsBytes(combinedMatrices.AsReadOnlySpan());
        writeOffset = ssboOffset * Unsafe.SizeOf<Matrix4x4>();

        primitiveStreamer.FrameSync.WaitAndLockRange(ssbo, section + writeOffset, span.Length);

        span.CopyTo(ssboSpan[(section + writeOffset)..]);
        GL.FlushMappedBufferRange(BufferTarget.ShaderStorageBuffer, section + writeOffset, span.Length);

        const MemoryBarrierFlags flags = MemoryBarrierFlags.ShaderStorageBarrierBit;
        if (supportsRegionalBarrier)
            GL.MemoryBarrierByRegion(Unsafe.BitCast<MemoryBarrierFlags, MemoryBarrierRegionFlags>(flags));
        else GL.MemoryBarrier(flags);
    }

    void BufferSSBOCompat()
    {
        var clipBytes = MemoryMarshal.AsBytes(clipRegions.AsReadOnlySpan());

        GL.BufferSubData(BufferTarget.ShaderStorageBuffer,
            ssboOffset * Unsafe.SizeOf<Vector4>(),
            clipBytes.Length,
            ref MemoryMarshal.GetReference(clipBytes));

        var section = maxQuadsPerBatch * Unsafe.SizeOf<Vector4>();

        if (Texture2d.BindlessTexturesSupported)
        {
            var texBytes = MemoryMarshal.AsBytes(bindlessTextures.AsReadOnlySpan());
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer,
                section + ssboOffset * sizeof(long),
                texBytes.Length,
                ref MemoryMarshal.GetReference(texBytes));

            section += maxQuadsPerBatch * sizeof(long);
        }

        var matBytes = MemoryMarshal.AsBytes(combinedMatrices.AsReadOnlySpan());

        GL.BufferSubData(BufferTarget.ShaderStorageBuffer,
            section + ssboOffset * Unsafe.SizeOf<Matrix4x4>(),
            matBytes.Length,
            ref MemoryMarshal.GetReference(matBytes));
    }

    Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);
        sb.AddRequiredExtension("GL_ARB_shader_draw_parameters", "GL_ARB_shader_storage_buffer_object");

        if (Texture2d.BindlessTexturesSupported) sb.AddRequiredExtension("GL_ARB_bindless_texture");

        var allSsbo = sb.AddSSBO(0);
        var ssboIndexOffset = sb.AddUniform(StorageBufferOffsetUniformName, ActiveUniformType.Int);

        var clipRects = allSsbo.FieldAsVariable(
            new(sb.Context, allSsbo.Name, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch),
            allSsbo.AddField(ClipUniformName, ActiveUniformType.FloatVec4, maxQuadsPerBatch));

        var texture = Texture2d.BindlessTexturesSupported ?
            allSsbo.FieldAsVariable(new(sb.Context, allSsbo.Name, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch),
                allSsbo.AddField(TextureUniformName, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch)) :
            sb.AddUniform(TextureUniformName, ActiveUniformType.Sampler2D);

        var combinedMatrix = allSsbo.FieldAsVariable(
            new(sb.Context, allSsbo.Name, ActiveUniformType.FloatMat4, maxQuadsPerBatch),
            allSsbo.AddField(CombinedMatrixUniformName, ActiveUniformType.FloatMat4, maxQuadsPerBatch));

        var color = sb.AddVarying(ActiveUniformType.FloatVec4);
        var textureCoord = sb.AddVarying(ActiveUniformType.FloatVec2);
        var drawId = sb.AddVarying(ActiveUniformType.Int);

        sb.VertexShader = new Sequence(new Assign(drawId, () => $"{sb.GlDrawID.Name} + {ssboIndexOffset.Ref}"),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)),
            new Assign(sb.GlPosition,
                ()
                    => $"{combinedMatrix.Ref[drawId.Ref]} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                    }, 0, 1)"),
            new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)));

        var clipRect = sb.AddFragmentVariable(ActiveUniformType.FloatVec4);

        sb.FragmentShader = new Sequence(new Assign(clipRect, () => $"{clipRects.Ref[drawId.Ref]}"),
            new Assign(sb.GlFragColor,
                () =>
                {
                    var texRef = Texture2d.BindlessTexturesSupported ?
                        $"sampler2D({texture.Ref[drawId.Ref]})" :
                        texture.Ref.ToString();

                    return
                        $"{color.Ref} * texture({texRef}, {textureCoord.Ref}) * float({sb.GlFragCoord.Ref}.x > {clipRect.Ref}.x && {sb.GlFragCoord.Ref}.x < ({clipRect.Ref}.x + {clipRect.Ref}.z) && {sb.GlFragCoord.Ref}.y > {clipRect.Ref}.y && {sb.GlFragCoord.Ref}.y < ({clipRect.Ref}.y + {clipRect.Ref}.w))";
                }));

        return sb.Build();
    }

    ~QuadRendererBuffered() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();
        GL.DeleteBuffer(ssbo);

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();

        if (Texture2d.BindlessTexturesSupported) bindlessTextures.Dispose();
        combinedMatrices.Dispose();
        clipRegions.Dispose();

        disposed = true;
    }
}