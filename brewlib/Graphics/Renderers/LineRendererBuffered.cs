namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using BrewLib.Util;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class LineRendererBuffered : ILineRenderer
{
    const int VertexPerLine = 2;
    const string CombinedMatrixUniformName = "u_combinedMatrix";

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition3d(),
        VertexAttribute.CreateColor(true));

    readonly bool ownsShader;

    readonly int maxLinesPerBatch;
    readonly IPrimitiveStreamer<LinePrimitive> primitiveStreamer;
    readonly ShaderUniform<Matrix4x4> combinedMatrixUniform;
    readonly Shader shader;

    ICamera camera;
    int linesInBatch;
    bool disposed, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;
    Matrix4x4 lastTransformMatrix;

    public PrimitiveTopology Topology => PrimitiveTopology.Lines;
    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
           PrimitiveBatchFeatures.Batched |
           PrimitiveBatchFeatures.VertexColored;

    public LineRendererBuffered(Shader shader = null, int maxLinesPerBatch = 64, int primitiveBufferSize = 0)
    {
        this.maxLinesPerBatch = maxLinesPerBatch;
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        combinedMatrixUniform = shader.GetUniform<Matrix4x4>(CombinedMatrixUniformName);

        primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<LinePrimitive>(VertexDeclaration,
            int.Max(maxLinesPerBatch, primitiveBufferSize / (VertexPerLine * VertexDeclaration.VertexSize)),
            default);

        SDL.LogInfo(LogCategory.Render,
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
        if (linesInBatch == 0) return;

        var combinedMatrix = transformMatrix * camera.ProjectionView;
        if (combinedMatrix != lastTransformMatrix)
        {
            combinedMatrixUniform.Set(combinedMatrix);
            lastTransformMatrix = combinedMatrix;
        }

        primitiveStreamer.Render(PrimitiveTopology.Lines, linesInBatch, VertexPerLine);
        linesInBatch = 0;
    }

    void ILineRenderer.Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Color color)
    {
        if (linesInBatch == maxLinesPerBatch) DrawState.FlushRenderer();

        var rgba = color.ToPixel<Rgba32>();
        primitiveStreamer.PrimitiveAt(linesInBatch) = new() { from = start, to = end, color1 = rgba, color2 = rgba };
        ++linesInBatch;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    static Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);

        var combinedMatrix = sb.AddUniform(CombinedMatrixUniformName, ShaderValueType.FloatMat4);

        var color = sb.AddVarying(ShaderValueType.FloatVec4);
        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(sb.GlPosition,
                ()
                    => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name}, 1)"));

        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref}"));

        return sb.Build();
    }

    ~LineRendererBuffered() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();

        if (!disposing) return;

        primitiveStreamer.Dispose();
        if (ownsShader) shader.Dispose();

        disposed = true;
    }
}
