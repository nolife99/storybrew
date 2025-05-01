namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cameras;
using Collections.Pooled;
using OpenTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using SixLabors.ImageSharp;
using Textures;

public class QuadRendererBuffered : IQuadRenderer
{
    const int VertexPerQuad = 6;

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
    readonly int ssbo, maxQuadsPerBatch;

    ICamera camera;
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

        var indicesCount = (int)(maxQuadsPerBatch * VertexPerQuad * 1.5f);
        if (indicesCount > ushort.MaxValue) throw new ArgumentException("Can't have more than 65535 indexed vertices");

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
            int.Max(maxQuadsPerBatch, primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize)) * VertexPerQuad,
            indices);

        GL.CreateBuffers(1, out ssbo);

        GL.NamedBufferStorage(ssbo,
            (Unsafe.SizeOf<Matrix4x4>() + sizeof(long) + Unsafe.SizeOf<Vector4>()) * maxQuadsPerBatch,
            0,
            BufferStorageFlags.DynamicStorageBit);

        combinedMatrices = new();
        bindlessTextures = new();
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

    public void BeginRendering()
    {
        shader.Begin();
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo);

        primitiveStreamer.Bind(shader);

        rendering = true;
    }

    public void EndRendering()
    {
        primitiveStreamer.Unbind();

        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0);
        shader.End();

        currentTextureHandle = 0;
        rendering = false;
    }

    public void Flush(bool canBuffer = false)
    {
        if (primitiveStreamer.PrimitivesInBatch != 0)
        {
            combinedMatrices.Add(Matrix4x4.Multiply(transformMatrix, camera.ProjectionView));
            bindlessTextures.Add(currentTextureHandle);

            var clipRegion = Rectangle.Intersect(DrawState.ClipRegion ?? Rectangle.Empty, DrawState.Viewport);
            if (clipRegion == Rectangle.Empty) clipRegion = DrawState.Viewport;

            clipRegions.Add(clipRegion);
            primitiveStreamer.QueueRender(VertexPerQuad);
        }

        var queuedRenders = primitiveStreamer.QueuedRenders;
        if (!canBuffer || queuedRenders == 0) return;

        // TODO: Buffer everything at once or map (will save ~15% frametime)

        GL.NamedBufferSubData(ssbo,
            0,
            queuedRenders * Unsafe.SizeOf<Matrix4x4>(),
            ref MemoryMarshal.GetReference(combinedMatrices.Span));

        combinedMatrices.Clear();

        GL.NamedBufferSubData(ssbo,
            maxQuadsPerBatch * Unsafe.SizeOf<Matrix4x4>(),
            queuedRenders * sizeof(long),
            ref MemoryMarshal.GetReference(bindlessTextures.Span));

        bindlessTextures.Clear();

        GL.NamedBufferSubData(ssbo,
            maxQuadsPerBatch * (sizeof(long) + Unsafe.SizeOf<Matrix4x4>()),
            queuedRenders * Unsafe.SizeOf<Vector4>(),
            ref MemoryMarshal.GetReference(clipRegions.Span));

        clipRegions.Clear();

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

        primitiveStreamer.AddPrimitive(in quad, VertexPerQuad);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region Default Shader

    Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);
        sb.AddRequiredExtension("GL_ARB_bindless_texture", "GL_ARB_shader_draw_parameters");

        var allSsbo = sb.AddSSBO(0);
        allSsbo.Restrict = true;
        allSsbo.ReadOnly = true;

        var combinedMatrix = allSsbo.FieldAsVariable(
            new(sb.Context, allSsbo.Name, ActiveUniformType.FloatMat4, maxQuadsPerBatch),
            allSsbo.AddField(CombinedMatrixUniformName, ActiveUniformType.FloatMat4, maxQuadsPerBatch));

        var texture =
            allSsbo.FieldAsVariable(new(sb.Context, allSsbo.Name, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch),
                allSsbo.AddField(TextureUniformName, ActiveUniformType.UnsignedIntVec2, maxQuadsPerBatch));

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
                ()
                    => $"{color.Ref} * texture(sampler2D({texture.Ref[drawId.Ref.ToString()]}), {textureCoord.Ref}) * float({sb.GlFragCoord.Ref}.x > {clipRect.Ref}.x && {sb.GlFragCoord.Ref}.x < ({clipRect.Ref}.x + {clipRect.Ref}.z) && {sb.GlFragCoord.Ref}.y > {clipRect.Ref}.y && {sb.GlFragCoord.Ref}.y < ({clipRect.Ref}.y + {clipRect.Ref}.w))"));

        return sb.Build();
    }

    #endregion

    ~QuadRendererBuffered() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) EndRendering();
        GL.DeleteBuffer(ssbo);

        combinedMatrices.Dispose();
        bindlessTextures.Dispose();
        clipRegions.Dispose();

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}