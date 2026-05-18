namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class LineRenderer : ILineRenderer
{
    const int VertexPerLine = 2;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4,
        ShaderBindingStage.Vertex,
        0);

    static readonly ShaderAttributeBinding VertexAttribute = new("a_vertex", ShaderValueType.Float);
    static readonly ShaderAttributeBinding FromAttribute = new("a_from", ShaderValueType.FloatVec3);
    static readonly ShaderAttributeBinding ToAttribute = new("a_to", ShaderValueType.FloatVec3);
    static readonly ShaderAttributeBinding ColorAttribute = new("a_color", ShaderValueType.FloatVec4);

    static readonly float[] UnitLineVertices = [0, 1];

    readonly IRenderPipeline pipeline;
    readonly IGraphicsBuffer vertexBuffer;
    readonly ITransientGraphicsBuffer instanceBuffer;
    readonly IRenderUniform<Matrix4x4> combinedMatrixUniform;
    readonly int instanceStride, instanceBatchCapacity;

    ICamera camera;
    bool disposed, rendering;
    Matrix4x4 transformMatrix = Matrix4x4.Identity;
    TransientBufferAllocation instanceAllocation;
    nint instanceData;
    int instanceCount;

    public LineRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 256)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instanceStride = Unsafe.SizeOf<LineInstance>();
        instanceBatchCapacity = int.Max(1, initialBatchCapacity);
        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(backend.ShaderSourceLanguage));
        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(LineRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.TransientBuffers.CreateBuffer(new(nameof(LineRenderer) + ".Instances",
                GraphicsBufferTarget.Vertex,
                GraphicsBufferUsage.Stream),
            getRingCapacity(instanceStride * instanceBatchCapacity));

        vertexBuffer.SetData<float>(UnitLineVertices);
        pipeline.BindVertexBuffer(0, vertexBuffer);
        pipeline.BindVertexBuffer(1, instanceBuffer.Buffer);

    }

    public PrimitiveTopology Topology => PrimitiveTopology.Lines;
    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
           PrimitiveBatchFeatures.Batched |
           PrimitiveBatchFeatures.Instanced |
           PrimitiveBatchFeatures.VertexColored;

    public Matrix4x4 TransformMatrix
    {
        get => transformMatrix;
        set
        {
            if (transformMatrix == value) return;

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
        pipeline.Bind();
        rendering = true;
    }

    void IRenderer.EndRendering()
    {
        if (instanceCount != 0) ((IRenderer)this).Flush();

        pipeline.Unbind();
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (instanceCount == 0) return;

        var usedBytes = instanceCount * instanceStride;
        instanceBuffer.Commit(in instanceAllocation, usedBytes);

        pipeline.Bind();

        combinedMatrixUniform.SetValue(transformMatrix * camera.ProjectionView);

        pipeline.BindVertexBuffer(1, instanceAllocation.Buffer, instanceAllocation.Offset);
        pipeline.DrawInstanced(new(VertexPerLine, instanceCount));
        DrawState.CountDrawCall();
        instanceBuffer.MarkSubmitted(in instanceAllocation, usedBytes);

        resetInstanceBatch();
    }

    void ILineRenderer.Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Color color)
    {
        if (instanceCount == instanceBatchCapacity)
            DrawState.FlushRenderer();

        ensureInstanceBatch();
        var rgba = color.ToPixel<Rgba32>();
        Unsafe.Add(ref instanceData.AsRef<LineInstance>(), instanceCount++) = new() { From = start, To = end, Color = rgba };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    static RenderPipelineDescription CreatePipelineDescription(ShaderSourceLanguage language)
    {
        var instanceStride = Unsafe.SizeOf<LineInstance>();

        return new(nameof(LineRenderer),
            createShaderSource(language),
            new(),
            new(
                new VertexBufferLayout(0,
                    Unsafe.SizeOf<float>(),
                    VertexInputRate.Vertex,
                    new VertexElement(VertexAttribute, VertexAttributeFormat.Float32, 0)),
                new VertexBufferLayout(1,
                    instanceStride,
                    VertexInputRate.Instance,
                    new VertexElement(FromAttribute, VertexAttributeFormat.Float32x3, 0),
                    new VertexElement(ToAttribute, VertexAttributeFormat.Float32x3, OffsetOf(nameof(LineInstance.To))),
                    new VertexElement(ColorAttribute, VertexAttributeFormat.Unorm8x4, OffsetOf(nameof(LineInstance.Color))))),
            PrimitiveTopology.Lines);
    }

    static ShaderProgramSource createShaderSource(ShaderSourceLanguage language)
        => language switch
        {
            ShaderSourceLanguage.Glsl => new(nameof(LineRenderer),
                createGlslVertexShader(),
                createGlslFragmentShader()),
            ShaderSourceLanguage.Hlsl => new(nameof(LineRenderer),
                createHlslVertexShader(),
                createHlslFragmentShader(),
                ShaderSourceLanguage.Hlsl),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };

    static string createGlslVertexShader()
        => """
           #version 150

           in float a_vertex;
           in vec3 a_from;
           in vec3 a_to;
           in vec4 a_color;

           uniform mat4 u_combinedMatrix;

           out vec4 v_color;

           void main()
           {
               vec3 position = mix(a_from, a_to, a_vertex);
               v_color = a_color;
               gl_Position = u_combinedMatrix * vec4(position, 1);
           }
           """;

    static string createGlslFragmentShader()
        => """
           #version 150

           in vec4 v_color;
           out vec4 fragColor;

           void main()
           {
               fragColor = v_color;
           }
           """;

    static string createHlslVertexShader()
        => """
           #pragma pack_matrix(row_major)

           cbuffer TransformUniforms : register(b0, space1)
           {
               float4x4 u_combinedMatrix;
           };

           struct VertexInput
           {
               [[vk::location(0)]] float Vertex : TEXCOORD0;
               [[vk::location(1)]] float3 From : TEXCOORD1;
               [[vk::location(2)]] float3 To : TEXCOORD2;
               [[vk::location(3)]] float4 Color : COLOR0;
           };

           struct VertexOutput
           {
               float4 Position : SV_Position;
               float4 Color : COLOR0;
           };

           VertexOutput main(VertexInput input)
           {
               VertexOutput output;
               float3 position = lerp(input.From, input.To, input.Vertex);
               output.Position = mul(float4(position, 1), u_combinedMatrix);
               output.Color = input.Color;
               return output;
           }
           """;

    static string createHlslFragmentShader()
        => """
           struct FragmentInput
           {
               float4 Position : SV_Position;
               float4 Color : COLOR0;
           };

           float4 main(FragmentInput input) : SV_Target0
           {
               return input.Color;
           }
           """;

    ~LineRenderer() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();

        vertexBuffer.Dispose();
        instanceBuffer.Dispose();
        pipeline.Dispose();

        disposed = true;
    }

    void ensureInstanceBatch()
    {
        if (instanceData != nint.Zero) return;

        instanceAllocation = instanceBuffer.Allocate(instanceStride * instanceBatchCapacity, 16);
        instanceData = instanceAllocation.Data;
    }

    void resetInstanceBatch()
    {
        instanceAllocation = default;
        instanceData = nint.Zero;
        instanceCount = 0;
    }

    static int OffsetOf(string fieldName)
        => (int)Marshal.OffsetOf<LineInstance>(fieldName);

    static int getRingCapacity(int batchSizeInBytes)
        => int.Max(checked(batchSizeInBytes * 8), 64 * 1024);

    [StructLayout(LayoutKind.Sequential)]
    struct LineInstance
    {
        public Vector3 From;
        public Vector3 To;
        public Rgba32 Color;
    }
}
