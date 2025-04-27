namespace BrewLib.Graphics.Renderers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cameras;
using Memory;
using OpenTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using Textures;

public class QuadRendererBuffered : IQuadRenderer
{
    const int VertexPerQuad = 6;
    const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture";

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(),
        VertexAttribute.CreateDiffuseCoord(),
        VertexAttribute.CreateColor(true));

    readonly bool ownsShader;

    readonly IPrimitiveStreamer<QuadPrimitive> primitiveStreamer;
    readonly Shader shader;

    ICamera camera;
    long currentTextureHandle;

    bool disposed, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;
    readonly UnmanagedList<Matrix4x4> combinedMatrices;
    readonly UnmanagedList<long> bindlessTextures;
    readonly int combinedMatricesBuffer, bindlessTexturesBuffer;

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 4096, int primitiveBufferSize = 0)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        var indicesCount = (int)(maxQuadsPerBatch * VertexPerQuad * 1.5f);
        if (indicesCount > ushort.MaxValue)
            throw new ArgumentException("Can't have more than 65535 indexed vertices!");

        Span<ushort> indices = stackalloc ushort[indicesCount];
        for (var i = 0; i < indicesCount / VertexPerQuad; ++i)
        {
            var triangleIndex = i * VertexPerQuad;
            var quadIndex = i * 4;

            indices[triangleIndex] = indices[triangleIndex + 5] = (ushort)quadIndex;
            indices[triangleIndex + 1] = (ushort)(quadIndex + 1);
            indices[triangleIndex + 2] = indices[triangleIndex + 3] = (ushort)(quadIndex + 2);
            indices[triangleIndex + 4] = (ushort)(quadIndex + 3);
        }

        primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<QuadPrimitive>(VertexDeclaration,
            int.Max(maxQuadsPerBatch,
                primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize)) *
            VertexPerQuad,
            indices);

        var buffers = new int[2];
        GL.CreateBuffers(2, buffers);

        GL.NamedBufferStorage(combinedMatricesBuffer = buffers[0], Unsafe.SizeOf<Matrix4x4>() * maxQuadsPerBatch, 0, BufferStorageFlags.DynamicStorageBit);
        GL.NamedBufferStorage(bindlessTexturesBuffer = buffers[1], sizeof(long) * maxQuadsPerBatch, 0, BufferStorageFlags.DynamicStorageBit);

        combinedMatrices = new();
        bindlessTextures = new();

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

    public void BeginRendering()
    {
        shader.Begin();
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, combinedMatricesBuffer);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, bindlessTexturesBuffer);

        primitiveStreamer.Bind(shader);

        rendering = true;
    }

    public void EndRendering()
    {
        primitiveStreamer.Unbind();

        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, 0);
        shader.End();

        currentTextureHandle = 0;
        rendering = false;
    }

    public void Flush(bool canBuffer = false)
    {
        if (primitiveStreamer.PrimitivesInBatch == 0) return;

        combinedMatrices.Add(Matrix4x4.Multiply(transformMatrix, camera.ProjectionView));
        bindlessTextures.Add(currentTextureHandle);
        primitiveStreamer.QueueRender(VertexPerQuad);

        if (!canBuffer) return;

        var queuedRenders = primitiveStreamer.QueuedRenders;

        GL.NamedBufferSubData(combinedMatricesBuffer, 0, Unsafe.SizeOf<Matrix4x4>() * queuedRenders, ref combinedMatrices.GetReference(0));
        combinedMatrices.Clear();

        GL.NamedBufferSubData(bindlessTexturesBuffer, 0, sizeof(long) * queuedRenders, ref bindlessTextures.GetReference(0));
        bindlessTextures.Clear();

        primitiveStreamer.Render(PrimitiveType.Triangles);
    }

    public void Draw(ref readonly QuadPrimitive quad, Texture2dRegion texture)
    {
        var textureId = texture.BindlessTextureHandle;
        if (currentTextureHandle != textureId)
        {
            DrawState.FlushRenderer();
            currentTextureHandle = textureId;
        }

        primitiveStreamer.AddPrimitive(in quad);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region Default Shader

    static Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);
        sb.AddRequiredExtension("GL_ARB_bindless_texture", "GL_ARB_shader_draw_parameters");

        var combinedMatrices = sb.AddSSBO(0);
        combinedMatrices.Restrict = true;
        combinedMatrices.ReadOnly = true;

        var combinedMatrix = combinedMatrices.FieldAsVariable(new(sb.Context, combinedMatrices.Name, ActiveUniformType.FloatMat4, 0), combinedMatrices.AddField(CombinedMatrixUniformName, ActiveUniformType.FloatMat4, 0));

        var textures = sb.AddSSBO(1);
        textures.Restrict = true;
        textures.ReadOnly = true;

        var texture = textures.FieldAsVariable(new(sb.Context, textures.Name, ActiveUniformType.UnsignedIntVec2, 0), textures.AddField(TextureUniformName, ActiveUniformType.UnsignedIntVec2, 0));

        var color = sb.AddVarying(ActiveUniformType.FloatVec4);
        var textureCoord = sb.AddVarying(ActiveUniformType.FloatVec2);
        var drawId = sb.AddVarying(ActiveUniformType.Int);

        sb.VertexShader = new Sequence(new Assign(drawId, () => sb.GlDrawID.Name),
            new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)),
            new Assign(sb.GlPosition,
                () => $"{combinedMatrix.Ref[sb.GlDrawID.Name]} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                }, 0, 1)"));

        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor,
            () => $"{color.Ref} * texture(sampler2D({texture.Ref[drawId.Ref.ToString()]}), {textureCoord.Ref})"));

        return sb.Build();
    }

    #endregion

    ~QuadRendererBuffered() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) EndRendering();
        GL.DeleteBuffer(combinedMatricesBuffer);
        GL.DeleteBuffer(bindlessTexturesBuffer);

        ((IDisposable)combinedMatrices).Dispose();
        ((IDisposable)bindlessTextures).Dispose();

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}