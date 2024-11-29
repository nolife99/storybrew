namespace BrewLib.Graphics.Renderers;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cameras;
using OpenTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

public class LineRendererBuffered : LineRenderer
{
    const int VertexPerLine = 2;
    const string CombinedMatrixUniformName = "u_combinedMatrix";

    static readonly VertexDeclaration VertexDeclaration =
        new(VertexAttribute.CreatePosition3d(), VertexAttribute.CreateColor(true));

    readonly int maxLinesPerBatch;
    readonly bool ownsShader;

    readonly MemoryManager<Int128> primitives;
    readonly PrimitiveStreamer<Int128> primitiveStreamer;
    readonly Shader shader;

    Camera camera;
    bool disposed, lastFlushWasBuffered, rendering;
    int linesInBatch;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public LineRendererBuffered(Shader shader = null, int maxLinesPerBatch = 7168, int primitiveBufferSize = 0)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<Int128>(VertexDeclaration,
            Math.Max(this.maxLinesPerBatch = maxLinesPerBatch,
                primitiveBufferSize / (VertexPerLine * VertexDeclaration.VertexSize)) * VertexPerLine,
            ReadOnlySpan<ushort>.Empty);

        primitives = (MemoryManager<Int128>)MemoryAllocator.Default.Allocate<Int128>(maxLinesPerBatch);
        Trace.WriteLine($"Initialized {nameof(LineRenderer)} using {primitiveStreamer.GetType().Name}");
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
            GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, ref combinedMatrix.M11);
        }

        primitiveStreamer.Render(PrimitiveType.Lines, primitives.GetSpan()[..linesInBatch], VertexPerLine);

        linesInBatch = 0;
        lastFlushWasBuffered = canBuffer;
    }

    public void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Rgba32 color)
    {
        if (linesInBatch == maxLinesPerBatch) DrawState.FlushRenderer(true);

        ref var ptr = ref Unsafe.As<Int128, byte>(ref primitives.GetSpan()[linesInBatch]);
        Unsafe.WriteUnaligned(ref ptr, start);
        Unsafe.WriteUnaligned(ref ptr = ref Unsafe.AddByteOffset(ref ptr, Marshal.SizeOf(start)), color);
        Unsafe.WriteUnaligned(ref ptr = ref Unsafe.AddByteOffset(ref ptr, Marshal.SizeOf(color)), end);
        Unsafe.WriteUnaligned(ref ptr = ref Unsafe.AddByteOffset(ref ptr, Marshal.SizeOf(end)), color);

        ++linesInBatch;
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

        ((IDisposable)primitives).Dispose();
        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}