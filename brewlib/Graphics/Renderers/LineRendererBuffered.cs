namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Numerics;
using Cameras;
using OpenTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using SixLabors.ImageSharp.PixelFormats;

public class LineRendererBuffered : ILineRenderer
{
    const int VertexPerLine = 2;
    const string CombinedMatrixUniformName = "u_combinedMatrix";

    static readonly VertexDeclaration VertexDeclaration =
        new(VertexAttribute.CreatePosition3d(), VertexAttribute.CreateColor(true));

    readonly int maxLinesPerBatch;
    readonly bool ownsShader;

    readonly IPrimitiveStreamer<LinePrimitive> primitiveStreamer;
    readonly Shader shader;

    Camera camera;
    bool disposed, lastFlushWasBuffered, rendering;
    int linesInBatch;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public LineRendererBuffered(Shader shader = null, int maxLinesPerBatch = 1024, int primitiveBufferSize = 0)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<LinePrimitive>(VertexDeclaration,
            int.Max(this.maxLinesPerBatch = maxLinesPerBatch,
                primitiveBufferSize / (VertexPerLine * VertexDeclaration.VertexSize)) *
            VertexPerLine,
            ReadOnlySpan<ushort>.Empty);

        Trace.WriteLine($"Initialized {nameof(LineRendererBuffered)} using {primitiveStreamer.GetType().Name}");
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
            var combinedMatrix = Matrix4x4.Multiply(transformMatrix, camera.ProjectionView);
            GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, ref combinedMatrix.M11);
        }

        primitiveStreamer.Render(PrimitiveType.Lines, linesInBatch, VertexPerLine);

        linesInBatch = 0;
        lastFlushWasBuffered = canBuffer;
    }

    public void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Rgba32 color)
    {
        if (linesInBatch == maxLinesPerBatch) DrawState.FlushRenderer(true);

        ref var ptr = ref primitiveStreamer.PrimitiveAt(linesInBatch);
        ptr.from = start;
        ptr.to = end;
        ptr.color1 = ptr.color2 = color;

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

        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(sb.GlPosition,
                () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
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

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}