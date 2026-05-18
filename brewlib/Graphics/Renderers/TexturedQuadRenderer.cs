namespace BrewLib.Graphics.Renderers;

using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using SixLabors.ImageSharp.PixelFormats;

public sealed class TexturedQuadRenderer : IQuadRenderer
{
    const int VertexPerQuad = 6;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4,
        ShaderBindingStage.Vertex,
        0);

    static readonly ShaderSamplerBinding TexturesSampler = new("u_textures");

    static readonly ShaderAttributeBinding VertexAttribute = new("a_vertex", ShaderValueType.FloatVec2);
    static readonly ShaderAttributeBinding TransformXAttribute = new("a_transformX", ShaderValueType.FloatVec2);
    static readonly ShaderAttributeBinding TransformYAttribute = new("a_transformY", ShaderValueType.FloatVec2);
    static readonly ShaderAttributeBinding TransformOriginAttribute = new("a_transformOrigin", ShaderValueType.FloatVec2);
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

    readonly TextureSlotter textureSlotter;
    readonly IRenderPipeline pipeline;
    readonly IResourceSet resources;
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

    public TexturedQuadRenderer(int initialBatchCapacity)
        : this(null, initialBatchCapacity)
    {
    }

    public TexturedQuadRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 8192)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instanceStride = Unsafe.SizeOf<TexturedQuadInstance>();
        instanceBatchCapacity = int.Max(1, initialBatchCapacity);
        textureSlotter = TextureSlotter.ForFragmentShader();

        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(backend.ShaderSourceLanguage,
            textureSlotter.Capacity));
        resources = pipeline.CreateResourceSet();
        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.TransientBuffers.CreateBuffer(new(nameof(TexturedQuadRenderer) + ".Instances",
                GraphicsBufferTarget.Vertex,
                GraphicsBufferUsage.Stream),
            getRingCapacity(instanceStride * instanceBatchCapacity));

        vertexBuffer.SetData<Vector2>(UnitQuadVertices);
        pipeline.BindVertexBuffer(0, vertexBuffer);
        pipeline.BindVertexBuffer(1, instanceBuffer.Buffer);

    }

    public PrimitiveTopology Topology => PrimitiveTopology.Triangles;
    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
           PrimitiveBatchFeatures.Batched |
           PrimitiveBatchFeatures.Instanced |
           PrimitiveBatchFeatures.Textured |
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
        textureSlotter.Clear();
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

        resources.SetTextures(0, textureSlotter.Textures);
        resources.Bind();

        pipeline.DrawInstanced(new(VertexPerQuad, instanceCount));
        DrawState.CountDrawCall();
        instanceBuffer.MarkSubmitted(in instanceAllocation, usedBytes);

        resetInstanceBatch();
        textureSlotter.Clear();
    }

    void IQuadRenderer.Draw(scoped ref readonly QuadPrimitive quad, ITextureRegion texture)
    {
        if (instanceCount == instanceBatchCapacity)
            DrawState.FlushRenderer();

        if (!textureSlotter.TryGetSlot(texture.Texture, out var textureSlot))
        {
            DrawState.FlushRenderer();
            if (!textureSlotter.TryGetSlot(texture.Texture, out textureSlot))
                throw new InvalidOperationException("Unable to allocate a texture slot for the quad batch");
        }

        ensureInstanceBatch();
        Unsafe.Add(ref instanceData.AsRef<TexturedQuadInstance>(), instanceCount++) = createInstance(in quad, textureSlot);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    static TexturedQuadInstance createInstance(scoped ref readonly QuadPrimitive quad, float textureSlot)
    {
        var origin = quad.vec1;
        var xAxis = quad.vec4 - origin;
        var yAxis = quad.vec2 - origin;

        return new()
        {
            Transform = new(xAxis.X, xAxis.Y, yAxis.X, yAxis.Y, origin.X, origin.Y),
            U = quad.u1,
            V = quad.v1,
            UAxis = (Half)((float)quad.u4 - (float)quad.u1),
            VAxis = (Half)((float)quad.v2 - (float)quad.v1),
            Color = quad.color1,
            TextureSlot = textureSlot
        };
    }

    static RenderPipelineDescription CreatePipelineDescription(ShaderSourceLanguage language, int textureSlots)
    {
        var instanceStride = Unsafe.SizeOf<TexturedQuadInstance>();

        return new(nameof(TexturedQuadRenderer),
            createShaderSource(language, textureSlots),
            new(new TextureBindingLayout(0, TexturesSampler, textureSlots)),
            new(
                new VertexBufferLayout(0,
                    Unsafe.SizeOf<Vector2>(),
                    VertexInputRate.Vertex,
                    new VertexElement(VertexAttribute, VertexAttributeFormat.Float32x2, 0)),
                new VertexBufferLayout(1,
                    instanceStride,
                    VertexInputRate.Instance,
                    new VertexElement(TransformXAttribute, VertexAttributeFormat.Float32x2, 0),
                    new VertexElement(TransformYAttribute, VertexAttributeFormat.Float32x2, 8),
                    new VertexElement(TransformOriginAttribute, VertexAttributeFormat.Float32x2, 16),
                    new VertexElement(UvAttribute, VertexAttributeFormat.Float16x4, OffsetOf(nameof(TexturedQuadInstance.U))),
                    new VertexElement(ColorAttribute, VertexAttributeFormat.Unorm8x4, OffsetOf(nameof(TexturedQuadInstance.Color))),
                    new VertexElement(TextureSlotAttribute,
                        VertexAttributeFormat.Float32,
                        OffsetOf(nameof(TexturedQuadInstance.TextureSlot))))),
            PrimitiveTopology.Triangles);
    }

    static ShaderProgramSource createShaderSource(ShaderSourceLanguage language, int textureSlots)
        => language switch
        {
            ShaderSourceLanguage.Glsl => new(nameof(TexturedQuadRenderer),
                createGlslVertexShader(),
                createGlslFragmentShader(textureSlots)),
            ShaderSourceLanguage.Hlsl => new(nameof(TexturedQuadRenderer),
                createHlslVertexShader(),
                createHlslFragmentShader(textureSlots),
                ShaderSourceLanguage.Hlsl),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };

    static string createGlslVertexShader()
        => """
           #version 150

           in vec2 a_vertex;
           in vec2 a_transformX;
           in vec2 a_transformY;
           in vec2 a_transformOrigin;
           in vec4 a_uv;
           in vec4 a_color;
           in float a_textureSlot;

           uniform mat4 u_combinedMatrix;

           out vec2 v_textureCoord;
           out vec4 v_color;
           flat out int v_textureSlot;

           void main()
           {
               vec2 position = a_transformOrigin + a_vertex.x * a_transformX + a_vertex.y * a_transformY;
               v_textureCoord = a_uv.xy + a_vertex * a_uv.zw;
               v_color = a_color;
               v_textureSlot = int(a_textureSlot + 0.5);
               gl_Position = u_combinedMatrix * vec4(position, 0, 1);
           }
           """;

    static string createGlslFragmentShader(int textureSlots)
    {
        StringBuilder shader = new();
        shader.AppendLine("#version 150");
        shader.AppendLine();
        shader.AppendLine("in vec2 v_textureCoord;");
        shader.AppendLine("in vec4 v_color;");
        shader.AppendLine("flat in int v_textureSlot;");
        shader.AppendLine(CultureInfo.InvariantCulture, $"uniform sampler2D {TexturesSampler.Name}[{textureSlots}];");
        shader.AppendLine("out vec4 fragColor;");
        shader.AppendLine();
        shader.AppendLine("void main()");
        shader.AppendLine("{");
        shader.AppendLine("    vec4 texel = vec4(0);");

        shader.AppendLine(CultureInfo.InvariantCulture,
            $"    if (v_textureSlot == 0) texel = texture({TexturesSampler.Name}[0], v_textureCoord);");
        for (var i = 1; i < textureSlots; ++i)
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"    else if (v_textureSlot == {i}) texel = texture({TexturesSampler.Name}[{i}], v_textureCoord);");

        shader.AppendLine("    fragColor = v_color * texel;");
        shader.AppendLine("}");
        return shader.ToString();
    }

    static string createHlslVertexShader()
        => """
           #pragma pack_matrix(row_major)

           cbuffer TransformUniforms : register(b0, space1)
           {
               float4x4 u_combinedMatrix;
           };

           struct VertexInput
           {
               [[vk::location(0)]] float2 Vertex : TEXCOORD0;
               [[vk::location(1)]] float2 TransformX : TEXCOORD1;
               [[vk::location(2)]] float2 TransformY : TEXCOORD2;
               [[vk::location(3)]] float2 TransformOrigin : TEXCOORD3;
               [[vk::location(4)]] float4 Uv : TEXCOORD4;
               [[vk::location(5)]] float4 Color : COLOR0;
               [[vk::location(6)]] float TextureSlot : TEXCOORD5;
           };

           struct VertexOutput
           {
               float4 Position : SV_Position;
               float2 TextureCoord : TEXCOORD0;
               float4 Color : COLOR0;
               nointerpolation int TextureSlot : TEXCOORD1;
           };

           VertexOutput main(VertexInput input)
           {
               VertexOutput output;
               float2 position = input.TransformOrigin + input.Vertex.x * input.TransformX + input.Vertex.y * input.TransformY;
               output.Position = mul(float4(position, 0, 1), u_combinedMatrix);
               output.TextureCoord = input.Uv.xy + input.Vertex * input.Uv.zw;
               output.Color = input.Color;
               output.TextureSlot = (int)(input.TextureSlot + 0.5);
               return output;
           }
           """;

    static string createHlslFragmentShader(int textureSlots)
    {
        StringBuilder shader = new();
        shader.AppendLine(CultureInfo.InvariantCulture,
            $"Texture2D<float4> {TexturesSampler.Name}[{textureSlots}] : register(t0, space2);");
        shader.AppendLine(CultureInfo.InvariantCulture, $"SamplerState u_samplers[{textureSlots}] : register(s0, space2);");
        shader.AppendLine();
        shader.AppendLine("struct FragmentInput");
        shader.AppendLine("{");
        shader.AppendLine("    float4 Position : SV_Position;");
        shader.AppendLine("    float2 TextureCoord : TEXCOORD0;");
        shader.AppendLine("    float4 Color : COLOR0;");
        shader.AppendLine("    nointerpolation int TextureSlot : TEXCOORD1;");
        shader.AppendLine("};");
        shader.AppendLine();
        shader.AppendLine("float4 main(FragmentInput input) : SV_Target0");
        shader.AppendLine("{");
        shader.AppendLine("    float4 texel = float4(0, 0, 0, 0);");

        shader.AppendLine(CultureInfo.InvariantCulture,
            $"    if (input.TextureSlot == 0) texel = {TexturesSampler.Name}[0].Sample(u_samplers[0], input.TextureCoord);");
        for (var i = 1; i < textureSlots; ++i)
            shader.AppendLine(CultureInfo.InvariantCulture,
                $"    else if (input.TextureSlot == {i}) texel = {TexturesSampler.Name}[{i}].Sample(u_samplers[{i}], input.TextureCoord);");

        shader.AppendLine("    return input.Color * texel;");
        shader.AppendLine("}");
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
        => (int)Marshal.OffsetOf<TexturedQuadInstance>(fieldName);

    static int getRingCapacity(int batchSizeInBytes)
        => int.Max(checked(batchSizeInBytes * 8), 1024 * 1024);

    [StructLayout(LayoutKind.Sequential)]
    struct TexturedQuadInstance
    {
        public Matrix3x2 Transform;
        public Half U, V, UAxis, VAxis;
        public Rgba32 Color;
        public float TextureSlot;
    }
}
