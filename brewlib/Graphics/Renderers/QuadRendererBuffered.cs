namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using BrewLib.Graphics.Textures;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;

public class QuadRendererBuffered : IQuadRenderer
{
    const int IndexPerQuad = 6, VertexPerQuad = 4;

    const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture",
        ClipUniformName = "u_clipRect";

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(),
        VertexAttribute.CreateDiffuseCoord(),
        VertexAttribute.CreateColor(true));

    readonly PooledList<long> bindlessTextures;
    readonly PooledList<Vector4> clipRegions;
    readonly PooledList<Matrix4x4> combinedMatrices;

    readonly bool ownsShader;

    readonly IPrimitiveStreamer<QuadPrimitive> primitiveStreamer;
    readonly Shader shader;
    readonly int ssbo, maxQuadsPerBatch, textureUniformLocation;

    ICamera camera;
    int currentTexture, currentSamplerUnit;
    long currentTextureHandle;

    bool disposed, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 8192, int primitiveBufferSize = 0)
    {
        this.maxQuadsPerBatch = maxQuadsPerBatch;
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        var ssboSize = (Unsafe.SizeOf<Matrix4x4>() + Unsafe.SizeOf<Vector4>()) * maxQuadsPerBatch;
        if (Texture2d.BindlessTexturesSupported)
        {
            ssboSize += sizeof(long) * maxQuadsPerBatch;
            bindlessTextures = new();
        }
        else textureUniformLocation = shader.GetUniformLocation(TextureUniformName);

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
        GL.BufferStorage(BufferTarget.ShaderStorageBuffer, ssboSize, 0, BufferStorageFlags.DynamicStorageBit);

        combinedMatrices = new();
        clipRegions = new();

        Trace.WriteLine($"Initialized {nameof(QuadRendererBuffered)} using {primitiveStreamer.GetType().Name}");
    }

    public Matrix4x4 TransformMatrix
    {
        get => transformMatrix;
        set
        {
            if (transformMatrix.Equals(value)) return;

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
        shader.Begin();
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo);

        primitiveStreamer.Bind(shader);

        rendering = true;
    }

    void IRenderer.EndRendering()
    {
        primitiveStreamer.Unbind();
        shader.End();

        currentTextureHandle = 0;
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (primitiveStreamer.PrimitivesInBatch != 0)
        {
            combinedMatrices.Add(Matrix4x4.Multiply(transformMatrix, camera.ProjectionView));
            if (Texture2d.BindlessTexturesSupported) bindlessTextures.Add(currentTextureHandle);

            var clipRegion = Rectangle.Intersect(DrawState.ClipRegion ?? Rectangle.Empty, DrawState.Viewport);
            if (clipRegion == Rectangle.Empty) clipRegion = DrawState.Viewport;

            clipRegions.Add(clipRegion);
            primitiveStreamer.QueueRender(IndexPerQuad, VertexPerQuad);
        }

        var queuedRenders = primitiveStreamer.QueuedRenders;
        if (!canBuffer || queuedRenders == 0) return;

        GL.BufferSubData(BufferTarget.ShaderStorageBuffer,
            0,
            queuedRenders * Unsafe.SizeOf<Matrix4x4>(),
            ref MemoryMarshal.GetReference(combinedMatrices.AsReadOnlySpan()));

        combinedMatrices.Clear();

        if (Texture2d.BindlessTexturesSupported)
        {
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer,
                maxQuadsPerBatch * Unsafe.SizeOf<Matrix4x4>(),
                queuedRenders * sizeof(long),
                ref MemoryMarshal.GetReference(bindlessTextures.AsReadOnlySpan()));

            bindlessTextures.Clear();
        }
        else
        {
            var samplerUnit = DrawState.BindTexture(currentTexture);
            if (currentSamplerUnit != samplerUnit)
            {
                GL.Uniform1(textureUniformLocation, samplerUnit);
                currentSamplerUnit = samplerUnit;
            }
        }

        GL.BufferSubData(BufferTarget.ShaderStorageBuffer,
            maxQuadsPerBatch * ((Texture2d.BindlessTexturesSupported ? sizeof(long) : 0) + Unsafe.SizeOf<Matrix4x4>()),
            queuedRenders * Unsafe.SizeOf<Vector4>(),
            ref MemoryMarshal.GetReference(clipRegions.AsReadOnlySpan()));

        clipRegions.Clear();

        primitiveStreamer.Render(PrimitiveType.Triangles, VertexPerQuad);
    }

    void IQuadRenderer.Draw(ref readonly QuadPrimitive quad, Texture2dRegion texture)
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

    Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);
        sb.AddRequiredExtension("GL_ARB_shader_draw_parameters", "GL_ARB_shader_storage_buffer_object");

        if (Texture2d.BindlessTexturesSupported) sb.AddRequiredExtension("GL_ARB_bindless_texture");

        var allSsbo = sb.AddSSBO(0);

        var combinedMatrix = allSsbo.FieldAsVariable(
            new(sb.Context, allSsbo.Name, ActiveUniformType.FloatMat4, maxQuadsPerBatch),
            allSsbo.AddField(CombinedMatrixUniformName, ActiveUniformType.FloatMat4, maxQuadsPerBatch));

        var texture = Texture2d.BindlessTexturesSupported ?
            allSsbo.FieldAsVariable(new(sb.Context, allSsbo.Name, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch),
                allSsbo.AddField(TextureUniformName, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch)) :
            sb.AddUniform(TextureUniformName, ActiveUniformType.Sampler2D);

        var clipRects =
            allSsbo.FieldAsVariable(new(sb.Context, allSsbo.Name, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch),
                allSsbo.AddField(ClipUniformName, ActiveUniformType.FloatVec4, maxQuadsPerBatch));

        var color = sb.AddVarying(ActiveUniformType.FloatVec4);
        var textureCoord = sb.AddVarying(ActiveUniformType.FloatVec2);
        var drawId = sb.AddVarying(ActiveUniformType.Int);

        sb.VertexShader = new Sequence(new Assign(drawId, () => sb.GlDrawID.Name),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)),
            new Assign(sb.GlPosition,
                ()
                    => $"{combinedMatrix.Ref[sb.GlDrawID.Name]} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                    }, 0, 1)"),
            new Assign(color, () => $"{sb.VertexDeclaration.GetAttribute(AttributeUsage.Color).Name}"));

        var clipRect = sb.AddFragmentVariable(ActiveUniformType.FloatVec4);

        sb.FragmentShader = new Sequence(new Assign(clipRect, () => $"{clipRects.Ref[drawId.Ref.ToString()]}"),
            new Assign(sb.GlFragColor,
                () =>
                {
                    var texRef = Texture2d.BindlessTexturesSupported ?
                        $"sampler2D({texture.Ref[drawId.Ref.ToString()]})" :
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

        combinedMatrices.Dispose();
        if (Texture2d.BindlessTexturesSupported) bindlessTextures.Dispose();
        clipRegions.Dispose();

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}