namespace BrewLib.Graphics.Renderers;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Backend;
using Cameras;
using Shaders;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public sealed class TexturedQuadRenderer : IQuadRenderer
{
    const int VertexPerQuad = 6;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4);

    static readonly ShaderSamplerBinding TexturesSampler = new("u_textures");

    static readonly ShaderAttributeBinding VertexAttribute = new("a_vertex", ShaderValueType.FloatVec2);
    static readonly ShaderAttributeBinding TransformAttribute = new("a_transform", ShaderValueType.FloatMat3x2);
    static readonly ShaderAttributeBinding UvAttribute = new("a_uv", ShaderValueType.FloatVec4);
    static readonly ShaderAttributeBinding ColorAttribute = new("a_color", ShaderValueType.FloatVec4);
    static readonly ShaderAttributeBinding TextureSlotAttribute = new("a_textureSlot", ShaderValueType.Float);

    static readonly Vector2[] UnitQuadVertices =
    [
        new(0, 0),
        new(0, 1),
        new(1, 1),
        new(0, 0),
        new(1, 1),
        new(1, 0)
    ];

    readonly IRenderUniform<Matrix4x4> combinedMatrixUniform;
    readonly IGraphicsBuffer instanceBuffer;
    readonly List<TexturedQuadInstance> instances;
    readonly IRenderPipeline pipeline;
    readonly IResourceSet resources;
    readonly IGraphicsBuffer vertexBuffer;

    ICamera camera;
    bool disposed, rendering;
    ITexture batchTexture;
    bool textureBatchHasSamplerIdentity;
    GraphicsResourceHandle textureBatchSamplerIdentity;
    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public TexturedQuadRenderer(int initialBatchCapacity)
        : this(null, initialBatchCapacity) { }

    public TexturedQuadRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 8192)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instances = new(initialBatchCapacity);
        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(backend.ShaderSourceLanguage,
            1,
            false,
            backend.Capabilities.Has(GraphicsBackendFeatures.ManualColorCorrection),
            backend.Capabilities.Has(GraphicsBackendFeatures.SrgbFramebuffer)));

        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);
        resources = pipeline.CreateResourceSet();

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.Buffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Instances",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Stream,
            Unsafe.SizeOf<TexturedQuadInstance>() * initialBatchCapacity));

        vertexBuffer.SetData(UnitQuadVertices);
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

    public PrimitiveTopology Topology => PrimitiveTopology.Triangles;

    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
            PrimitiveBatchFeatures.Batched |
            PrimitiveBatchFeatures.Instanced |
            PrimitiveBatchFeatures.Textured |
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
        rendering = true;
    }

    void IRenderer.EndRendering()
    {
        if (instances.Count != 0) drawCurrentBatch();
        clearTextureBatchState();
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (instances.Count == 0) return;

        drawCurrentBatch();
    }

    void IQuadRenderer.Draw(scoped ref readonly QuadInstance source, ITextureRegion texture)
    {
        var textureResource = texture.Texture;
        if (batchTexture is not null && !ReferenceEquals(batchTexture, textureResource))
            DrawState.FlushRenderer(true);

        if (shouldSplitBatchForSampler(textureResource))
            DrawState.FlushRenderer(true);

        batchTexture ??= textureResource;
        trackSamplerState(textureResource);

        instances.Add(new()
        {
            Transform = source.Transform,
            U = source.U,
            V = source.V,
            UAxis = source.UAxis,
            VAxis = source.VAxis,
            Color = source.Color,
            TextureSlot = 0
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void drawCurrentBatch()
    {
        if (batchTexture is null)
            throw new InvalidOperationException("Cannot draw a textured quad batch without a texture");

        var span = CollectionsMarshal.AsSpan(instances);
        instanceBuffer.SetData(span);

        combinedMatrixUniform.SetValue(transformMatrix * camera.ProjectionView);

        resources.SetTextures(TexturesSampler, [batchTexture]);
        pipeline.DrawInstanced(new(VertexPerQuad, span.Length), [new(0, vertexBuffer), new(1, instanceBuffer)], resources);

        instances.Clear();
        clearTextureBatchState();
    }

    bool shouldSplitBatchForSampler(ITexture texture)
        => textureBatchHasSamplerIdentity &&
            texture is ITextureSamplerIdentity samplerIdentity &&
            samplerIdentity.SamplerIdentity != textureBatchSamplerIdentity;

    void trackSamplerState(ITexture texture)
    {
        if (textureBatchHasSamplerIdentity || texture is not ITextureSamplerIdentity samplerIdentity)
            return;

        textureBatchSamplerIdentity = samplerIdentity.SamplerIdentity;
        textureBatchHasSamplerIdentity = true;
    }

    void clearTextureBatchState()
    {
        batchTexture = null;
        textureBatchSamplerIdentity = default;
        textureBatchHasSamplerIdentity = false;
    }

    static RenderPipelineDescription CreatePipelineDescription(ShaderSourceLanguage language,
        int textureSlots,
        bool useNonUniformTextureIndexing,
        bool useManualColorCorrection,
        bool useSrgbFramebuffer)
    {
        var instanceStride = Unsafe.SizeOf<TexturedQuadInstance>();

        return new(nameof(TexturedQuadRenderer),
            createShaderSource(language, textureSlots, useNonUniformTextureIndexing, useManualColorCorrection, useSrgbFramebuffer),
            new(new TextureBindingLayout(TexturesSampler, textureSlots)),
            new(
                new VertexBufferLayout(0,
                    Unsafe.SizeOf<Vector2>(),
                    VertexInputRate.Vertex,
                    new VertexElement(VertexAttribute, VertexAttributeFormat.Float32x2, 0)),
                new VertexBufferLayout(1,
                    instanceStride,
                    VertexInputRate.Instance,
                    new VertexElement(TransformAttribute, VertexAttributeFormat.Float32Mat3x2, 0),
                    new VertexElement(UvAttribute, VertexAttributeFormat.Float16x4, OffsetOf(nameof(TexturedQuadInstance.U))),
                    new VertexElement(ColorAttribute, VertexAttributeFormat.Unorm8x4, OffsetOf(nameof(TexturedQuadInstance.Color))),
                    new VertexElement(TextureSlotAttribute,
                        VertexAttributeFormat.Float32,
                        OffsetOf(nameof(TexturedQuadInstance.TextureSlot))))),
            PrimitiveTopology.Triangles);
    }

    static ShaderProgramSource createShaderSource(ShaderSourceLanguage language,
        int textureSlots,
        bool useNonUniformTextureIndexing,
        bool useManualColorCorrection,
        bool useSrgbFramebuffer)
        => language switch
        {
            ShaderSourceLanguage.Wgsl => new(nameof(TexturedQuadRenderer),
                createWgslVertexShader(),
                createWgslFragmentShader(textureSlots, useNonUniformTextureIndexing, useSrgbFramebuffer),
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
               @location(0) vertex: vec2<f32>,
               @location(1) transform0: vec2<f32>,
               @location(2) transform1: vec2<f32>,
               @location(3) transform2: vec2<f32>,
               @location(4) uv: vec4<f32>,
               @location(5) color: vec4<f32>,
               @location(6) texture_slot: f32,
           };

           struct VertexOutput {
               @builtin(position) position: vec4<f32>,
               @location(0) texture_coord: vec2<f32>,
               @location(1) color: vec4<f32>,
               @location(2) @interpolate(flat) texture_slot: u32,
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
               let position = input.transform0 * input.vertex.x +
                   input.transform1 * input.vertex.y +
                   input.transform2;
               output.position = transform_position(vec4<f32>(position, 0.0, 1.0));
               output.texture_coord = input.uv.xy + input.vertex * input.uv.zw;
               output.color = input.color;
               output.texture_slot = u32(input.texture_slot + 0.5);
               return output;
           }
           """;

    static string createWgslFragmentShader(int textureSlots,
        bool useNonUniformTextureIndexing,
        bool useSrgbFramebuffer)
    {
        var colorHelpers = useSrgbFramebuffer
            ? """
              fn srgb_to_linear_channel(value: f32) -> f32 {
                  let v = clamp(value, 0.0, 1.0);
                  return select(pow((v + 0.055) / 1.055, 2.4), v / 12.92, v <= 0.04045);
              }

              fn input_color_to_linear(color: vec4<f32>) -> vec4<f32> {
                  return vec4<f32>(
                      srgb_to_linear_channel(color.r),
                      srgb_to_linear_channel(color.g),
                      srgb_to_linear_channel(color.b),
                      color.a);
              }
              """
            : """
              fn input_color_to_linear(color: vec4<f32>) -> vec4<f32> {
                  return color;
              }
              """;

        if (useNonUniformTextureIndexing)
            return $$"""
                     {{colorHelpers}}

                     @group(1) @binding(0) var u_textures: binding_array<texture_2d<f32>, {{textureSlots}}>;
                     @group(1) @binding(1) var u_sampler: sampler;

                     struct FragmentInput {
                         @location(0) texture_coord: vec2<f32>,
                         @location(1) color: vec4<f32>,
                         @location(2) @interpolate(flat) texture_slot: u32,
                     };

                     @fragment
                     fn main(input: FragmentInput) -> @location(0) vec4<f32> {
                         let texture_slot = input.texture_slot;
                         let texel = textureSample(u_textures[texture_slot], u_sampler, input.texture_coord);
                         return input_color_to_linear(input.color) * texel;
                     }
                     """;

        StringBuilder shader = new();
        shader.AppendLine(colorHelpers);
        shader.AppendLine();

        for (var i = 0; i < textureSlots; ++i)
        {
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"@group(1) @binding({i * 2}) var u_texture{i}: texture_2d<f32>;");

            shader.AppendLine(CultureInfo.InvariantCulture,
                $"@group(1) @binding({i * 2 + 1}) var u_sampler{i}: sampler;");
        }

        shader.AppendLine();
        shader.AppendLine("""
                          struct FragmentInput {
                              @location(0) texture_coord: vec2<f32>,
                              @location(1) color: vec4<f32>,
                              @location(2) @interpolate(flat) texture_slot: u32,
                          };

                          fn sample_texture(texture_slot: u32, texture_coord: vec2<f32>) -> vec4<f32> {
                          """);

        for (var i = 0; i < textureSlots; ++i)
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"    if (texture_slot == {i}u) {{ return textureSample(u_texture{i}, u_sampler{i}, texture_coord); }}");

        shader.AppendLine("""
                              return vec4<f32>(0.0, 0.0, 0.0, 0.0);
                          }

                          @fragment
                          fn main(input: FragmentInput) -> @location(0) vec4<f32> {
                              let texel = sample_texture(input.texture_slot, input.texture_coord);
                              return input_color_to_linear(input.color) * texel;
                          }
                          """);

        return shader.ToString();
    }

    ~TexturedQuadRenderer() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;

        if (rendering) ((IRenderer)this).EndRendering();

        resources.Dispose();
        vertexBuffer.Dispose();
        instanceBuffer.Dispose();
        pipeline.Dispose();

        disposed = true;
    }

    static int OffsetOf(string fieldName)
        => (int)Marshal.OffsetOf<TexturedQuadInstance>(fieldName);

    [StructLayout(LayoutKind.Sequential)]
    struct TexturedQuadInstance
    {
        public Matrix3x2 Transform;
        public Half U, V, UAxis, VAxis;
        public Rgba32 Color;
        public float TextureSlot;
    }
}