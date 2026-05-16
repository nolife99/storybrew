namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using SDL3;
using SixLabors.ImageSharp.PixelFormats;

public sealed class OpenGlInstancedQuadRenderer : IQuadRenderer
{
    const int VertexPerQuad = 6;

    static readonly ShaderUniformBinding<Matrix4x4> CombinedMatrixUniform = new("u_combinedMatrix",
        ShaderValueType.FloatMat4);

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

    readonly List<InstancedQuadInstance> instances;
    readonly TextureSlotter textureSlotter;
    readonly IRenderPipeline pipeline;
    readonly IResourceSet resources;
    readonly IGraphicsBuffer vertexBuffer, instanceBuffer;
    readonly IRenderUniform<Matrix4x4> combinedMatrixUniform;

    ICamera camera;
    bool disposed, rendering;
    Matrix4x4 transformMatrix = Matrix4x4.Identity;
    Matrix4x4 lastTransformMatrix;

    public OpenGlInstancedQuadRenderer(int initialBatchCapacity)
        : this(null, initialBatchCapacity)
    {
    }

    public OpenGlInstancedQuadRenderer(IGraphicsBackend backend = null, int initialBatchCapacity = 8192)
    {
        if (initialBatchCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBatchCapacity), initialBatchCapacity, null);

        backend ??= DrawState.Backend ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating renderers");

        instances = new(initialBatchCapacity);
        textureSlotter = TextureSlotter.ForFragmentShader();

        pipeline = backend.RenderPipelines.CreateRenderPipeline(CreatePipelineDescription(textureSlotter.Capacity));
        resources = pipeline.CreateResourceSet();
        combinedMatrixUniform = pipeline.GetUniform(CombinedMatrixUniform);

        vertexBuffer = backend.Buffers.CreateBuffer(new(nameof(OpenGlInstancedQuadRenderer) + ".Vertices",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Static));

        instanceBuffer = backend.Buffers.CreateBuffer(new(nameof(OpenGlInstancedQuadRenderer) + ".Instances",
            GraphicsBufferTarget.Vertex,
            GraphicsBufferUsage.Stream));

        vertexBuffer.SetData<Vector2>(UnitQuadVertices);
        pipeline.BindVertexBuffer(0, vertexBuffer);
        pipeline.BindVertexBuffer(1, instanceBuffer);

        SDL.LogInfo(LogCategory.Render,
            $"Initialized {nameof(OpenGlInstancedQuadRenderer)} using {textureSlotter.Capacity} texture slots");
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
        if (instances.Count != 0) ((IRenderer)this).Flush();

        pipeline.Unbind();
        textureSlotter.Clear();
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        var instanceCount = instances.Count;
        if (instanceCount == 0) return;

        var combinedMatrix = transformMatrix * camera.ProjectionView;
        if (combinedMatrix != lastTransformMatrix)
        {
            combinedMatrixUniform.SetValue(combinedMatrix);
            lastTransformMatrix = combinedMatrix;
        }

        resources.SetTextures(0, textureSlotter.Textures);
        resources.Bind();

        var instanceData = CollectionsMarshal.AsSpan(instances);
        instanceBuffer.SetData(instanceData);
        pipeline.BindVertexBuffer(1, instanceBuffer);

        pipeline.DrawInstanced(new(VertexPerQuad, instanceCount));
        DrawState.CountDrawCall();

        instanceBuffer.Invalidate();

        instances.Clear();
        textureSlotter.Clear();
    }

    void IQuadRenderer.Draw(scoped ref readonly QuadPrimitive quad, ITextureRegion texture)
    {
        if (!textureSlotter.TryGetSlot(texture.Texture, out var textureSlot))
        {
            DrawState.FlushRenderer();
            if (!textureSlotter.TryGetSlot(texture.Texture, out textureSlot))
                throw new InvalidOperationException("Unable to allocate a texture slot for the quad batch");
        }

        instances.Add(createInstance(in quad, textureSlot));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    static InstancedQuadInstance createInstance(scoped ref readonly QuadPrimitive quad, float textureSlot)
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

    static RenderPipelineDescription CreatePipelineDescription(int textureSlots)
    {
        var instanceStride = Unsafe.SizeOf<InstancedQuadInstance>();

        return new(nameof(OpenGlInstancedQuadRenderer),
            new(nameof(OpenGlInstancedQuadRenderer), createVertexShader(), createFragmentShader(textureSlots)),
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
                    new VertexElement(UvAttribute, VertexAttributeFormat.Float16x4, OffsetOf(nameof(InstancedQuadInstance.U))),
                    new VertexElement(ColorAttribute, VertexAttributeFormat.Unorm8x4, OffsetOf(nameof(InstancedQuadInstance.Color))),
                    new VertexElement(TextureSlotAttribute,
                        VertexAttributeFormat.Float32,
                        OffsetOf(nameof(InstancedQuadInstance.TextureSlot))))),
            PrimitiveTopology.Triangles);
    }

    static string createVertexShader()
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

    static string createFragmentShader(int textureSlots)
    {
        StringBuilder shader = new();
        shader.AppendLine("#version 150");
        shader.AppendLine();
        shader.AppendLine("in vec2 v_textureCoord;");
        shader.AppendLine("in vec4 v_color;");
        shader.AppendLine("flat in int v_textureSlot;");
        shader.AppendLine($"uniform sampler2D {TexturesSampler.Name}[{textureSlots}];");
        shader.AppendLine("out vec4 fragColor;");
        shader.AppendLine();
        shader.AppendLine("void main()");
        shader.AppendLine("{");
        shader.AppendLine("    vec4 texel = vec4(0);");

        shader.AppendLine($"    if (v_textureSlot == 0) texel = texture({TexturesSampler.Name}[0], v_textureCoord);");
        for (var i = 1; i < textureSlots; ++i)
            shader.AppendLine($"    else if (v_textureSlot == {i}) texel = texture({TexturesSampler.Name}[{i}], v_textureCoord);");

        shader.AppendLine("    fragColor = v_color * texel;");
        shader.AppendLine("}");
        return shader.ToString();
    }

    ~OpenGlInstancedQuadRenderer() => Dispose(false);

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
        => (int)Marshal.OffsetOf<InstancedQuadInstance>(fieldName);

    [StructLayout(LayoutKind.Sequential)]
    struct InstancedQuadInstance
    {
        public Matrix3x2 Transform;
        public Half U, V, UAxis, VAxis;
        public Rgba32 Color;
        public float TextureSlot;
    }
}
