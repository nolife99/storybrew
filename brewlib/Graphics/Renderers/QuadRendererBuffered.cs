namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using BrewLib.Graphics.Textures;
using SDL3;
using SixLabors.ImageSharp.Memory;

public sealed class QuadRendererBuffered : IQuadRenderer
{
    const int IndexPerQuad = 6, VertexPerQuad = 4;

    const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture";

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(false),
        VertexAttribute.CreateDiffuseCoord(true),
        VertexAttribute.CreateColor(true));

    readonly bool ownsShader;

    readonly int maxQuadsPerBatch;
    readonly IPrimitiveStreamer<QuadPrimitive> primitiveStreamer;
    readonly ShaderUniform<Matrix4x4> combinedMatrixUniform;
    readonly ShaderUniform<int> textureUniform;
    readonly Shader shader;

    ICamera camera;
    ITexture currentTexture;
    int quadsInBatch;

    bool disposed, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;
    Matrix4x4 lastTransformMatrix;

    public PrimitiveTopology Topology => PrimitiveTopology.Triangles;
    public PrimitiveBatchFeatures BatchFeatures
        => PrimitiveBatchFeatures.PainterOrdered |
           PrimitiveBatchFeatures.Batched |
           PrimitiveBatchFeatures.Indexed |
           PrimitiveBatchFeatures.Textured |
           PrimitiveBatchFeatures.VertexColored;

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 7168, int primitiveBufferSize = 0)
    {
        this.maxQuadsPerBatch = maxQuadsPerBatch;
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        combinedMatrixUniform = shader.GetUniform<Matrix4x4>(CombinedMatrixUniformName);
        textureUniform = shader.GetUniform<int>(TextureUniformName);

        var indicesCount = maxQuadsPerBatch * IndexPerQuad;
        if (indicesCount > ushort.MaxValue)
            throw new InvalidOperationException($"Too many quads: {indicesCount} indices > {ushort.MaxValue}");

        using (var indicesBuffer = MemoryAllocator.Default.Allocate<ushort>(indicesCount))
        {
            var indices = indicesBuffer.Memory.Span;
            for (var i = 0; i < maxQuadsPerBatch; ++i)
            {
                var triangleIndex = i * IndexPerQuad;
                var quadIndex = i * VertexPerQuad;

                indices[triangleIndex] = indices[triangleIndex + 5] = (ushort)quadIndex;
                indices[triangleIndex + 1] = (ushort)(quadIndex + 1);
                indices[triangleIndex + 2] = indices[triangleIndex + 3] = (ushort)(quadIndex + 2);
                indices[triangleIndex + 4] = (ushort)(quadIndex + 3);
            }

            primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<QuadPrimitive>(VertexDeclaration,
                int.Max(maxQuadsPerBatch, primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize)),
                indices);
        }

        SDL.LogInfo(LogCategory.Render,
            $"Initialized {nameof(QuadRendererBuffered)} using {primitiveStreamer.GetType().Name}");
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

        currentTexture = null;
        rendering = false;
    }

    void IRenderer.Flush(bool canBuffer)
    {
        if (quadsInBatch == 0) return;

        var combinedMatrix = transformMatrix * camera.ProjectionView;
        if (combinedMatrix != lastTransformMatrix)
        {
            combinedMatrixUniform.Set(combinedMatrix);
            lastTransformMatrix = combinedMatrix;
        }

        var samplerUnit = DrawState.BindTexture(currentTexture);
        textureUniform.Set(samplerUnit);

        primitiveStreamer.Render(PrimitiveTopology.Triangles, quadsInBatch, IndexPerQuad);
        quadsInBatch = 0;
    }

    void IQuadRenderer.Draw(scoped ref readonly QuadPrimitive quad, ITextureRegion texture)
    {
        if (!ReferenceEquals(currentTexture, texture.Texture))
        {
            DrawState.FlushRenderer();
            currentTexture = texture.Texture;
        }
        else if (quadsInBatch == maxQuadsPerBatch) DrawState.FlushRenderer();

        primitiveStreamer.PrimitiveAt(quadsInBatch) = quad;
        ++quadsInBatch;
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
        var texture = sb.AddUniform(TextureUniformName, ShaderValueType.Sampler2D);

        var color = sb.AddVarying(ShaderValueType.FloatVec4);
        var textureCoord = sb.AddVarying(ShaderValueType.FloatVec2);

        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)),
            new Assign(sb.GlPosition,
                ()
                    => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name}, 0, 1)"));

        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor,
            () => $"{color.Ref} * texture({texture.Ref}, {textureCoord.Ref})"));

        return sb.Build();
    }

    ~QuadRendererBuffered() => Dispose(false);

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
