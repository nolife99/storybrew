namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cameras;
using osuTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using Util;

public unsafe class LineRendererBuffered : LineRenderer
{
    public const int VertexPerLine = 2;
    public const string CombinedMatrixUniformName = "u_combinedMatrix";

    public static readonly VertexDeclaration VertexDeclaration =
        new(VertexAttribute.CreatePosition3d(), VertexAttribute.CreateColor(true));

    readonly int maxLinesPerBatch;
    readonly bool ownsShader;

    Camera camera;
    bool disposed, lastFlushWasBuffered, rendering;
    int linesInBatch, currentLargestBatch;

    void* primitives;
    PrimitiveStreamer primitiveStreamer;
    Shader shader;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public LineRendererBuffered(Shader shader = null, int maxLinesPerBatch = 4096, int primitiveBufferSize = 0) : this(
        PrimitiveStreamerUtil<Int128>.DefaultCreatePrimitiveStreamer, shader, maxLinesPerBatch, primitiveBufferSize) { }

    public LineRendererBuffered(Func<VertexDeclaration, int, PrimitiveStreamer> createPrimitiveStreamer,
        Shader shader,
        int maxLinesPerBatch,
        int primitiveBufferSize)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.maxLinesPerBatch = maxLinesPerBatch;
        this.shader = shader;

        var batchSize = Math.Max(maxLinesPerBatch, primitiveBufferSize / (VertexPerLine * VertexDeclaration.VertexSize));
        primitiveStreamer = createPrimitiveStreamer(VertexDeclaration, batchSize * VertexPerLine);

        primitives = NativeMemory.Alloc((nuint)(maxLinesPerBatch * Marshal.SizeOf<Int128>()));
        Trace.WriteLine($"Initialized {nameof(LineRenderer)} using {primitiveStreamer.GetType().Name}");
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

    public int RenderedLineCount { get; set; }
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

        rendering = false;
    }
    public void Flush(bool canBuffer = false)
    {
        if (linesInBatch == 0) return;
        if (!lastFlushWasBuffered)
        {
            var combinedMatrix = transformMatrix * camera.ProjectionView;
            GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, &combinedMatrix.M11);
        }

        primitiveStreamer.Render(PrimitiveType.Lines, primitives, linesInBatch, linesInBatch * VertexPerLine, canBuffer);

        currentLargestBatch += linesInBatch;
        if (!canBuffer)
        {
            LargestBatch = Math.Max(LargestBatch, currentLargestBatch);
            currentLargestBatch = 0;
        }

        linesInBatch = 0;
        ++FlushedBufferCount;
        lastFlushWasBuffered = canBuffer;
    }

    public void Draw(Vector3 start, Vector3 end, Color color) => Draw(start, end, color, color);
    public void Draw(Vector3 start, Vector3 end, Color startColor, Color endColor)
    {
        if (linesInBatch == maxLinesPerBatch) DrawState.FlushRenderer(true);

        var ptr = (int*)Unsafe.Add<Int128>(primitives, linesInBatch);
        Unsafe.Write(ptr, start);
        Unsafe.Write(ptr + 3, startColor.ToRgba());
        Unsafe.Write(ptr + 4, end);
        Unsafe.Write(ptr + 7, endColor.ToRgba());

        ++RenderedLineCount;
        ++linesInBatch;
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
        var color = sb.AddVarying("vec4");

        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)), new Assign(
            sb.GlPosition, () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
            }, 1)"));

        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref}"));

        return sb.Build();
    }

    #endregion

    ~LineRendererBuffered() => Dispose(false);
    void Dispose(bool disposing)
    {
        if (disposed) return;
        if (rendering) EndRendering();

        NativeMemory.Free(primitives);
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
}