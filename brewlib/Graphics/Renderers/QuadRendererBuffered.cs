namespace BrewLib.Graphics.Renderers;

using System;
using System.Diagnostics;
using System.Numerics;
using Cameras;
using OpenTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using Textures;

public class QuadRendererBuffered : QuadRenderer
{
    const int VertexPerQuad = 6;
    const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture";

    static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(),
        VertexAttribute.CreateDiffuseCoord(),
        VertexAttribute.CreateColor(true));

    readonly int maxQuadsPerBatch, textureUniformLocation;
    readonly bool ownsShader;

    readonly PrimitiveStreamer<QuadPrimitive> primitiveStreamer;
    readonly Shader shader;

    Camera camera;
    int currentSamplerUnit, quadsInBatch, currentTexture;

    bool disposed, lastFlushWasBuffered, rendering;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public QuadRendererBuffered(Shader shader = null, int maxQuadsPerBatch = 7168, int primitiveBufferSize = 0)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.shader = shader;

        textureUniformLocation = shader.GetUniformLocation(TextureUniformName);

        const float iboFactor = 1.5f;
        // Generate an index buffer to render 1 quad as 2 triangles
        // any factor below 1.5x is too small, causing GL to not draw anything

        Span<ushort> indices = stackalloc ushort[(int)(maxQuadsPerBatch * VertexPerQuad * iboFactor)];
        for (var i = 0; i < maxQuadsPerBatch * iboFactor; ++i)
        {
            var triangleIndex = i * VertexPerQuad;
            var quadIndex = i * 4;

            indices[triangleIndex] = indices[triangleIndex + 5] = (ushort)quadIndex;
            indices[triangleIndex + 1] = (ushort)(quadIndex + 1);
            indices[triangleIndex + 2] = indices[triangleIndex + 3] = (ushort)(quadIndex + 2);
            indices[triangleIndex + 4] = (ushort)(quadIndex + 3);
        }

        primitiveStreamer = PrimitiveStreamerUtil.DefaultCreatePrimitiveStreamer<QuadPrimitive>(VertexDeclaration,
            Math.Max(this.maxQuadsPerBatch = maxQuadsPerBatch,
                primitiveBufferSize / (VertexPerQuad * VertexDeclaration.VertexSize)) *
            VertexPerQuad,
            indices);

        Trace.WriteLine($"Initialized {nameof(QuadRenderer)} using {primitiveStreamer.GetType().Name}");
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

        currentTexture = 0;
        rendering = false;
    }

    public void Flush(bool canBuffer = false)
    {
        if (quadsInBatch == 0) return;

        // When the previous flush was bufferable, draw state should stay the same.
        if (!lastFlushWasBuffered)
        {
            var combinedMatrix = Matrix4x4.Multiply(transformMatrix, camera.ProjectionView);
            GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, ref combinedMatrix.M11);

            var samplerUnit = DrawState.BindTexture(currentTexture);
            if (currentSamplerUnit != samplerUnit)
            {
                GL.Uniform1(textureUniformLocation, samplerUnit);
                currentSamplerUnit = samplerUnit;
            }
        }

        primitiveStreamer.Render(PrimitiveType.Triangles, quadsInBatch, VertexPerQuad);

        quadsInBatch = 0;
        lastFlushWasBuffered = canBuffer;
    }
    public void Draw(ref readonly QuadPrimitive quad, Texture2dRegion texture)
    {
        var textureId = texture.BindableTexture.TextureId;
        if (currentTexture != textureId)
        {
            DrawState.FlushRenderer();
            currentTexture = textureId;
        }
        else if (quadsInBatch == maxQuadsPerBatch) DrawState.FlushRenderer(true);

        primitiveStreamer.PrimitiveAt(quadsInBatch) = quad;
        ++quadsInBatch;
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
        var texture = sb.AddUniform(TextureUniformName, "sampler2D");

        var color = sb.AddVarying("vec4");
        var textureCoord = sb.AddVarying("vec2");

        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)),
            new Assign(sb.GlPosition,
                () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                }, 0, 1)"));

        sb.FragmentShader =
            new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref} * texture2D({texture.Ref}, {textureCoord.Ref})"));

        return sb.Build();
    }

    #endregion

    ~QuadRendererBuffered() => Dispose(false);
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