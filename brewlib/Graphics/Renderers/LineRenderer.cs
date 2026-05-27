namespace BrewLib.Graphics.Renderers;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Backend;
using Cameras;
using Shaders;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class LineRenderer : ILineRenderer
{
    const int VertexPerLine = 2;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4);

    static readonly ShaderAttributeBinding VertexAttribute = new("a_vertex", ShaderValueType.Float);
    static readonly ShaderAttributeBinding FromAttribute = new("a_from", ShaderValueType.FloatVec3);
    static readonly ShaderAttributeBinding ToAttribute = new("a_to", ShaderValueType.FloatVec3);
    static readonly ShaderAttributeBinding ColorAttribute = new("a_color", ShaderValueType.FloatVec4);

    static readonly float[] UnitLineVertices = [0, 1];

    readonly IRenderUniform<Matrix4x4> combinedMatrixUniform;
    readonly IGraphicsBuffer instanceBuffer;
    readonly List<LineInstance> instances;
    readonly IRenderPipeline pipeline;
    readonly IGraphicsBuffer vertexBuffer;

    ICamera camera;
    bool disposed, rendering;
    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public LineRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 256)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instances = new(initialBatchCapacity);

        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(backend.ShaderSourceLanguage,
            backend.Capabilities.Has(GraphicsBackendFeatures.ManualColorCorrection),
            backend.Capabilities.Has(GraphicsBackendFeatures.SrgbFramebuffer)));

        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(LineRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.Buffers.CreateBuffer(new(nameof(LineRenderer) + ".Instances",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Stream,
            Unsafe.SizeOf<LineInstance>() * initialBatchCapacity));

        vertexBuffer.SetData(UnitLineVertices);
        pipeline.BindVertexBuffer(0, vertexBuffer);
    }

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

    public PrimitiveTopology Topology => PrimitiveTopology.Lines;

    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
            PrimitiveBatchFeatures.Batched |
            PrimitiveBatchFeatures.Instanced |
            PrimitiveBatchFeatures.VertexColored;

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
        if (instances.Count != 0) drawCurrentBatch();
        pipeline.Unbind();
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (instances.Count == 0) return;
        drawCurrentBatch();
    }

    void ILineRenderer.Draw(scoped ref readonly Vector3 start,
        scoped ref readonly Vector3 end,
        scoped ref readonly Color color)
    {
        instances.Add(new LineInstance
        {
            From = start,
            To = end,
            Color = color.ToPixel<Rgba32>()
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void drawCurrentBatch()
    {
        var span = CollectionsMarshal.AsSpan(instances);
        instanceBuffer.SetData(span);

        combinedMatrixUniform.SetValue(transformMatrix * camera.ProjectionView);
        pipeline.BindVertexBuffer(1, instanceBuffer);
        pipeline.DrawInstanced(new(VertexPerLine, span.Length));

        instances.Clear();
    }

    static RenderPipelineDescription CreatePipelineDescription(ShaderSourceLanguage language,
        bool useManualColorCorrection,
        bool useSrgbFramebuffer)
    {
        var instanceStride = Unsafe.SizeOf<LineInstance>();

        return new(nameof(LineRenderer),
            createShaderSource(language, useManualColorCorrection, useSrgbFramebuffer),
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

    static ShaderProgramSource createShaderSource(ShaderSourceLanguage language,
        bool useManualColorCorrection,
        bool useSrgbFramebuffer)
        => language switch
        {
            ShaderSourceLanguage.Wgsl => new(nameof(LineRenderer),
                createWgslVertexShader(),
                createWgslFragmentShader(useSrgbFramebuffer),
                ShaderSourceLanguage.Wgsl),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };

    static string createWgslVertexShader()
        => """
           struct TransformUniforms {
               rows: array<vec4<f32>, 4>,
           };

           @group(0) @binding(0) var<uniform> u_transform: TransformUniforms;

           struct VertexInput {
               @location(0) vertex: f32,
               @location(1) line_start: vec3<f32>,
               @location(2) line_end: vec3<f32>,
               @location(3) color: vec4<f32>,
           };

           struct VertexOutput {
               @builtin(position) position: vec4<f32>,
               @location(0) color: vec4<f32>,
           };

           fn transform_position(position: vec4<f32>) -> vec4<f32> {
               return vec4<f32>(
                   position.x * u_transform.rows[0].x + position.y * u_transform.rows[1].x + position.z * u_transform.rows[2].x + position.w * u_transform.rows[3].x,
                   position.x * u_transform.rows[0].y + position.y * u_transform.rows[1].y + position.z * u_transform.rows[2].y + position.w * u_transform.rows[3].y,
                   position.x * u_transform.rows[0].z + position.y * u_transform.rows[1].z + position.z * u_transform.rows[2].z + position.w * u_transform.rows[3].z,
                   position.x * u_transform.rows[0].w + position.y * u_transform.rows[1].w + position.z * u_transform.rows[2].w + position.w * u_transform.rows[3].w);
           }

           @vertex
           fn main(input: VertexInput) -> VertexOutput {
               var output: VertexOutput;
               let position = mix(input.line_start, input.line_end, input.vertex);
               output.position = transform_position(vec4<f32>(position, 1.0));
               output.color = input.color;
               return output;
           }
           """;

    static string createWgslFragmentShader(bool useSrgbFramebuffer)
        => useSrgbFramebuffer
            ? """
              struct FragmentInput {
                  @location(0) color: vec4<f32>,
              };

              fn srgb_to_linear_channel(value: f32) -> f32 {
                  let v = clamp(value, 0.0, 1.0);
                  return select(pow((v + 0.055) / 1.055, 2.4), v / 12.92, v <= 0.04045);
              }

              fn srgb_to_linear(color: vec3<f32>) -> vec3<f32> {
                  return vec3<f32>(
                      srgb_to_linear_channel(color.r),
                      srgb_to_linear_channel(color.g),
                      srgb_to_linear_channel(color.b));
              }

              @fragment
              fn main(input: FragmentInput) -> @location(0) vec4<f32> {
                  return vec4<f32>(srgb_to_linear(input.color.rgb), input.color.a);
              }
              """
            : """
              struct FragmentInput {
                  @location(0) color: vec4<f32>,
              };

              @fragment
              fn main(input: FragmentInput) -> @location(0) vec4<f32> {
                  return input.color;
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

    static int OffsetOf(string fieldName)
        => (int)Marshal.OffsetOf<LineInstance>(fieldName);

    [StructLayout(LayoutKind.Sequential)]
    struct LineInstance
    {
        public Vector3 From;
        public Vector3 To;
        public Rgba32 Color;
    }
}
