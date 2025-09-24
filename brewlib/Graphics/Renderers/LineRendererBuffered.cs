namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using OpenTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;

public sealed class LineRendererBuffered : ILineRenderer
{
    const int VertexPerLine = 2;
    const string CombinedMatrixUniformName = "u_combinedMatrix";

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition3d(),
        VertexAttribute.CreateColor(true));

    readonly PooledList<Matrix4x4> combinedMatrices;
    readonly int combinedMatricesBuffer;

    readonly bool ownsShader;

    readonly IPrimitiveStreamer<LinePrimitive> primitiveStreamer;
    readonly Shader shader;

    ICamera camera;
    bool disposed, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public LineRendererBuffered(Shader shader = null, int maxLinesPerBatch = 64, int primitiveBufferSize = 0)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<LinePrimitive>(VertexDeclaration,
            int.Max(maxLinesPerBatch, primitiveBufferSize / (VertexPerLine * VertexDeclaration.VertexSize)),
            default);

        combinedMatricesBuffer = GL.GenBuffer();
        combinedMatrices = new();

        SDL.LogInfo(SDL.LogCategory.Render,
            $"Initialized {nameof(LineRendererBuffered)} using {primitiveStreamer.GetType().Name}");
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
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, combinedMatricesBuffer);

        primitiveStreamer.Bind(shader);

        rendering = true;
    }

    void IRenderer.EndRendering()
    {
        primitiveStreamer.Unbind();
        shader.End();

        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (primitiveStreamer.PrimitivesInBatch != 0)
        {
            combinedMatrices.Add(transformMatrix * camera.ProjectionView);
            primitiveStreamer.QueueRender(VertexPerLine, VertexPerLine);
        }

        var queuedRenders = primitiveStreamer.QueuedRenders;
        if (!canBuffer || queuedRenders == 0) return;

        var ssboWritten = Unsafe.SizeOf<Matrix4x4>() * queuedRenders;
        GL.BufferData(BufferTarget.ShaderStorageBuffer,
            ssboWritten,
            ref MemoryMarshal.GetReference(combinedMatrices.AsReadOnlySpan()),
            BufferUsageHint.StaticDraw);

        combinedMatrices.Clear();

        primitiveStreamer.Render(PrimitiveType.Lines, VertexPerLine);

        if (DrawState.CanInvalidate) GL.InvalidateBufferData(combinedMatricesBuffer);
    }

    void ILineRenderer.Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Color color)
    {
        var rgba = color.ToPixel<Rgba32>();
        LinePrimitive primitive = new() { from = start, to = end, color1 = rgba, color2 = rgba };
        primitiveStreamer.AddPrimitive(in primitive);
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

        var combinedMatrices = sb.AddSSBO();
        var combinedMatrix = combinedMatrices.FieldAsVariable(
            new(sb.Context, combinedMatrices.Name, ActiveUniformType.FloatMat4, 0),
            combinedMatrices.AddField(CombinedMatrixUniformName, ActiveUniformType.FloatMat4, 0));

        var color = sb.AddVarying(ActiveUniformType.FloatVec4);
        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(sb.GlPosition,
                ()
                    => $"{combinedMatrix.Ref[sb.GlDrawId.Name]} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                    }, 1)"));

        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref}"));

        return sb.Build();
    }

    #endregion

    ~LineRendererBuffered() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();
        GL.DeleteBuffer(combinedMatricesBuffer);

        combinedMatrices.Dispose();

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();
        disposed = true;
    }
}