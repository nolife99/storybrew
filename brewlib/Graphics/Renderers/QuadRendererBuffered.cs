using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using BrewLib.Graphics.Textures;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics.Renderers;

public class QuadRendererBuffered : QuadRenderer
{
    public const int VertexPerQuad = 4;
    public const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture";

    public static readonly VertexDeclaration VertexDeclaration =
        new(VertexAttribute.CreatePosition2d(), VertexAttribute.CreateDiffuseCoord(0), VertexAttribute.CreateColor(true));

    #region Default Shader

    public static Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);

        var combinedMatrix = sb.AddUniform(CombinedMatrixUniformName, "mat4");
        var texture = sb.AddUniform(TextureUniformName, "sampler2D");

        var color = sb.AddVarying("vec4");
        var textureCoord = sb.AddVarying("vec2");

        sb.VertexShader = new Sequence(
            new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)),
            new Assign(sb.GlPosition, () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name}, 0, 1)")
        );
        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref} * texture2D({texture.Ref}, {textureCoord.Ref})"));

        return sb.Build();
    }

    #endregion

    Shader shader;
    readonly int combinedMatrixLocation, textureUniformLocation;

    public Shader Shader => ownsShader ? null : shader;
    readonly bool ownsShader;

    PrimitiveStreamer<QuadPrimitive> primitiveStreamer;
    QuadPrimitive[] primitives;

    Camera camera;
    public Camera Camera
    {
        get => camera;
        set
        {
            if (camera == value) return;

            if (rendering) DrawState.FlushRenderer();
            camera = value;
        }
    }

    Matrix4x4 transformMatrix = Matrix4x4.Identity;
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

    int quadsInBatch;
    readonly int maxQuadsPerBatch;

    BindableTexture currentTexture;
    int currentSamplerUnit, currentLargestBatch;
    bool rendering;

    public int RenderedQuadCount { get; set; }
    public int FlushedBufferCount { get; set; }
    public int DiscardedBufferCount => primitiveStreamer.DiscardedBufferCount;
    public int BufferWaitCount => primitiveStreamer.BufferWaitCount;
    public int LargestBatch { get; set; }

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 4096, int primitiveBufferSize = 0)
        : this(PrimitiveStreamerUtil<QuadPrimitive>.DefaultCreatePrimitiveStreamer, shader, maxQuadsPerBatch, primitiveBufferSize) { }

    public QuadRendererBuffered(Func<VertexDeclaration, int, PrimitiveStreamer<QuadPrimitive>> createPrimitiveStreamer, Shader shader, int maxQuadsPerBatch, int primitiveBufferSize)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.maxQuadsPerBatch = maxQuadsPerBatch;
        this.shader = shader;

        combinedMatrixLocation = shader.GetUniformLocation(CombinedMatrixUniformName);
        textureUniformLocation = shader.GetUniformLocation(TextureUniformName);

        var primitiveBatchSize = Math.Max(maxQuadsPerBatch, primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize));
        primitiveStreamer = createPrimitiveStreamer(VertexDeclaration, primitiveBatchSize * VertexPerQuad);

        primitives = ArrayPool<QuadPrimitive>.Shared.Rent(maxQuadsPerBatch);
        Trace.WriteLine($"Initialized {nameof(QuadRenderer)} using {primitiveStreamer.GetType().Name}");
    }
    public void BeginRendering()
    {
        if (rendering) throw new InvalidOperationException("Already rendering");

        shader.Begin();
        primitiveStreamer.Bind(shader);

        rendering = true;
    }
    public void EndRendering()
    {
        if (!rendering) throw new InvalidOperationException("Not rendering");

        primitiveStreamer.Unbind();
        shader.End();

        currentTexture = null;
        rendering = false;
    }

    bool lastFlushWasBuffered;
    public void Flush(bool canBuffer = false)
    {
        if (quadsInBatch == 0) return;
        if (currentTexture is null) throw new InvalidOperationException("currentTexture is null");

        // When the previous flush was bufferable, draw state should stay the same.
        if (!lastFlushWasBuffered) unsafe
        {
            var combinedMatrix = transformMatrix * camera.ProjectionView;
            GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, &combinedMatrix.M11);

            var samplerUnit = DrawState.BindTexture(currentTexture);
            if (currentSamplerUnit != samplerUnit)
            {
                currentSamplerUnit = samplerUnit;
                GL.Uniform1(textureUniformLocation, currentSamplerUnit);
            }
        }

        primitiveStreamer.Render(PrimitiveType.Quads, primitives, quadsInBatch, quadsInBatch * VertexPerQuad, canBuffer);

        currentLargestBatch += quadsInBatch;
        if (!canBuffer)
        {
            LargestBatch = Math.Max(LargestBatch, currentLargestBatch);
            currentLargestBatch = 0;
        }

        quadsInBatch = 0;
        ++FlushedBufferCount;

        lastFlushWasBuffered = canBuffer;
    }

    bool disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    void Dispose(bool disposing)
    {
        if (disposed) return;
        if (rendering) EndRendering();

        ArrayPool<QuadPrimitive>.Shared.Return(primitives);
        if (disposing)
        {
            primitives = null;
            camera = null;

            primitiveStreamer.Dispose();
            primitiveStreamer = null;

            if (ownsShader) shader.Dispose();
            shader = null;
            disposed = true;
        }
    }

    public void Draw(ref QuadPrimitive quad, Texture2dRegion texture)
    {
        if (!rendering) throw new InvalidOperationException("Not rendering");
        if (currentTexture != texture.BindableTexture)
        {
            DrawState.FlushRenderer();
            currentTexture = texture.BindableTexture;
        }
        else if (quadsInBatch == maxQuadsPerBatch) DrawState.FlushRenderer(true);

        primitives[quadsInBatch] = quad;

        ++RenderedQuadCount;
        ++quadsInBatch;
    }
}