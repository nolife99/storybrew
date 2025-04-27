namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Cameras;
using Memory;
using OpenTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using SixLabors.ImageSharp.PixelFormats;

public class LineRendererBuffered : ILineRenderer
{
    const int VertexPerLine = 2;
    const string CombinedMatrixUniformName = "u_combinedMatrix";

    static readonly VertexDeclaration VertexDeclaration = new(
        VertexAttribute.CreatePosition3d(),
        VertexAttribute.CreateColor(true));

    readonly UnmanagedList<Matrix4x4> combinedMatrices;
    readonly int combinedMatricesBuffer;

    readonly int maxLinesPerBatch;
    readonly bool ownsShader;

    readonly IPrimitiveStreamer<LinePrimitive> primitiveStreamer;
    readonly Shader shader;

    ICamera camera;
    bool disposed, rendering;

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

        GL.CreateBuffers(1, out combinedMatricesBuffer);
        GL.NamedBufferStorage(combinedMatricesBuffer,
            Unsafe.SizeOf<Matrix4x4>() * maxLinesPerBatch,
            0,
            BufferStorageFlags.DynamicStorageBit);

        combinedMatrices = new();

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
        if (primitiveStreamer.PrimitivesInBatch == 0) return;

        combinedMatrices.Add(Matrix4x4.Multiply(transformMatrix, camera.ProjectionView));
        primitiveStreamer.QueueRender(VertexPerLine);

        if (!canBuffer) return;

        GL.NamedBufferSubData(combinedMatricesBuffer,
            0,
            Unsafe.SizeOf<Matrix4x4>() * primitiveStreamer.QueuedRenders,
            ref combinedMatrices.GetReference(0));

        combinedMatrices.Clear();

        primitiveStreamer.Render(PrimitiveType.Lines);
    }

    public void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Rgba32 color)
    {
        LinePrimitive primitive = new() { from = start, to = end, color1 = color, color2 = color };
        primitiveStreamer.AddPrimitive(ref primitive);
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
        sb.AddRequiredExtension("GL_ARB_shader_draw_parameters");

        var combinedMatrices = sb.AddSSBO(0);
        var combinedMatrix = combinedMatrices.FieldAsVariable(
            new(sb.Context, combinedMatrices.Name, ActiveUniformType.FloatMat4, 0),
            combinedMatrices.AddField(CombinedMatrixUniformName, ActiveUniformType.FloatMat4, 0));

        var color = sb.AddVarying(ActiveUniformType.FloatVec4);
        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(sb.GlPosition,
                ()
                    => $"{combinedMatrix.Ref[sb.GlDrawID.Name]} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
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
        GL.DeleteBuffer(combinedMatricesBuffer);

        ((IDisposable)combinedMatrices).Dispose();

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}