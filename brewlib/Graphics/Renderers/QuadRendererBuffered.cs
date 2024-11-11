namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cameras;
using osuTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using Textures;

public unsafe class QuadRendererBuffered : QuadRenderer
{
    const int VertexPerQuad = 4;
    const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture";

    public static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(),
        VertexAttribute.CreateDiffuseCoord(), VertexAttribute.CreateColor(true));

    readonly int maxQuadsPerBatch, textureUniformLocation;
    readonly bool ownsShader;

    Camera camera;
    int currentSamplerUnit, currentLargestBatch, quadsInBatch;
    Texture2d currentTexture;

    bool disposed, lastFlushWasBuffered, rendering;

    void* primitives;
    PrimitiveStreamer primitiveStreamer;
    Shader shader;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 4096, int primitiveBufferSize = 0) : this(
        PrimitiveStreamerUtil<QuadPrimitive>.DefaultCreatePrimitiveStreamer, shader, maxQuadsPerBatch, primitiveBufferSize) { }

    QuadRendererBuffered(Func<VertexDeclaration, int, PrimitiveStreamer> createPrimitiveStreamer,
        Shader shader,
        int maxQuadsPerBatch,
        int primitiveBufferSize)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.maxQuadsPerBatch = maxQuadsPerBatch;
        this.shader = shader;

        textureUniformLocation = shader.GetUniformLocation(TextureUniformName);

        var primitiveBatchSize = Math.Max(maxQuadsPerBatch, primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize));
        primitiveStreamer = createPrimitiveStreamer(VertexDeclaration, primitiveBatchSize * VertexPerQuad);

        primitives = NativeMemory.Alloc((nuint)(maxQuadsPerBatch * Marshal.SizeOf<QuadPrimitive>()));
        Trace.WriteLine($"Initialized {nameof(QuadRenderer)} using {primitiveStreamer.GetType().Name}");
    }

    public Shader Shader => ownsShader ? null : shader;

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

    public int RenderedQuadCount { get; set; }
    public int FlushedBufferCount { get; set; }
    public int DiscardedBufferCount => primitiveStreamer.DiscardedBufferCount;
    public int BufferWaitCount => primitiveStreamer.BufferWaitCount;
    public int LargestBatch { get; set; }

    public void BeginRendering()
    {
        shader.Begin();
        primitiveStreamer.Bind(shader);

        rendering = true;
    }
    public void EndRendering()
    {
        primitiveStreamer.Unbind();
        shader.End();

        currentTexture = null;
        rendering = false;
    }

    public void Flush(bool canBuffer = false)
    {
        if (quadsInBatch == 0) return;

        // When the previous flush was bufferable, draw state should stay the same.
        if (!lastFlushWasBuffered)
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
    public void Draw(ref QuadPrimitive quad, Texture2dRegion texture)
    {
        if (currentTexture != texture.BindableTexture)
        {
            DrawState.FlushRenderer();
            currentTexture = texture.BindableTexture;
        }
        else if (quadsInBatch == maxQuadsPerBatch) DrawState.FlushRenderer(true);

        Unsafe.Write(Unsafe.Add<QuadPrimitive>(primitives, quadsInBatch), quad);

        ++RenderedQuadCount;
        ++quadsInBatch;
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region Default Shader

    public static Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);

        var combinedMatrix = sb.AddUniform(CombinedMatrixUniformName, "mat4");
        var texture = sb.AddUniform(TextureUniformName, "sampler2D");

        var color = sb.AddVarying("vec4");
        var textureCoord = sb.AddVarying("vec2");

        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)), new Assign(sb.GlPosition,
                () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                }, 0, 1)"));

        sb.FragmentShader =
            new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref} * texture2D({texture.Ref}, {textureCoord.Ref})"));

        return sb.Build();
    }

    #endregion

    ~QuadRendererBuffered() => Dispose(false);
    void Dispose(bool disposing)
    {
        if (disposed) return;
        if (rendering) EndRendering();

        NativeMemory.Free(primitives);
        if (!disposing) return;

        primitives = null;
        camera = null;

        primitiveStreamer.Dispose();
        primitiveStreamer = null;

        if (ownsShader) shader.Dispose();
        shader = null;
        disposed = true;
    }
}