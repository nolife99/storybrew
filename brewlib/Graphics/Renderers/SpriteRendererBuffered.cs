﻿namespace BrewLib.Graphics.Renderers;

using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Cameras;
using osuTK.Graphics.OpenGL;
using PrimitiveStreamers;
using Shaders;
using Shaders.Snippets;
using Textures;
using Util;

public unsafe class SpriteRendererBuffered : SpriteRenderer
{
    public const int VertexPerSprite = 4;
    public const string CombinedMatrixUniformName = "u_combinedMatrix", TextureUniformName = "u_texture";

    public static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition2d(),
        VertexAttribute.CreateDiffuseCoord(), VertexAttribute.CreateColor(true));

    readonly int maxSpritesPerBatch;
    readonly bool ownsShader;

    Camera camera;
    int currentLargestBatch, currentSamplerUnit, spritesInBatch;

    BindableTexture currentTexture;
    bool disposed, lastFlushWasBuffered, rendering;

    void* primitives;
    PrimitiveStreamer primitiveStreamer;
    Shader shader;

    Matrix4x4 transformMatrix = Matrix4x4.Identity;

    public SpriteRendererBuffered(Shader shader = null,
        Action flushAction = null,
        int maxSpritesPerBatch = 4096,
        int primitiveBufferSize = 0) : this(PrimitiveStreamerUtil<QuadPrimitive>.DefaultCreatePrimitiveStreamer, shader,
        flushAction, maxSpritesPerBatch, primitiveBufferSize) { }

    public SpriteRendererBuffered(Func<VertexDeclaration, int, PrimitiveStreamer> createPrimitiveStreamer,
        Shader shader,
        Action flushAction,
        int maxSpritesPerBatch,
        int primitiveBufferSize)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.maxSpritesPerBatch = maxSpritesPerBatch;
        FlushAction = flushAction;
        this.shader = shader;

        var primitiveBatchSize = Math.Max(maxSpritesPerBatch,
            (int)((float)primitiveBufferSize / (VertexPerSprite * VertexDeclaration.VertexSize)));

        primitiveStreamer = createPrimitiveStreamer(VertexDeclaration, primitiveBatchSize * VertexPerSprite);

        primitives = NativeMemory.Alloc((nuint)(maxSpritesPerBatch * Marshal.SizeOf<QuadPrimitive>()));
    }

    public Action FlushAction { get; set; }
    public Shader Shader => ownsShader ? null : shader;

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

    public int RenderedSpriteCount { get; private set; }
    public int FlushedBufferCount { get; private set; }
    public int DiscardedBufferCount => primitiveStreamer.DiscardedBufferCount;
    public int BufferWaitCount => primitiveStreamer.BufferWaitCount;
    public int LargestBatch { get; private set; }

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

        currentTexture = null;
        rendering = false;
    }

    public void Flush(bool canBuffer = false)
    {
        if (spritesInBatch == 0) return;

        // When the previous flush was bufferable, draw state should stay the same.
        if (!lastFlushWasBuffered)
        {
            var combinedMatrix = transformMatrix * camera.ProjectionView;

            var samplerUnit = DrawState.BindTexture(currentTexture);
            if (currentSamplerUnit != samplerUnit)
            {
                currentSamplerUnit = samplerUnit;
                GL.Uniform1(shader.GetUniformLocation(TextureUniformName), currentSamplerUnit);
            }

            GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, &combinedMatrix.M11);
            FlushAction?.Invoke();
        }

        primitiveStreamer.Render(PrimitiveType.Quads, primitives, spritesInBatch, spritesInBatch * VertexPerSprite, canBuffer);

        currentLargestBatch += spritesInBatch;
        if (!canBuffer)
        {
            LargestBatch = Math.Max(LargestBatch, currentLargestBatch);
            currentLargestBatch = 0;
        }

        spritesInBatch = 0;
        ++FlushedBufferCount;
        lastFlushWasBuffered = canBuffer;
    }

    public void Draw(Texture2dRegion texture,
        float x,
        float y,
        float originX,
        float originY,
        float scaleX,
        float scaleY,
        float rotation,
        Color color) => Draw(texture, x, y, originX, originY, scaleX, scaleY, rotation, color, 0, 0, texture.Width,
        texture.Height);

    public void Draw(Texture2dRegion texture,
        float x,
        float y,
        float originX,
        float originY,
        float scaleX,
        float scaleY,
        float rotation,
        Color color,
        float textureX0,
        float textureY0,
        float textureX1,
        float textureY1)
    {
        if (currentTexture != texture.BindableTexture)
        {
            DrawState.FlushRenderer();
            currentTexture = texture.BindableTexture;
        }
        else if (spritesInBatch == maxSpritesPerBatch) DrawState.FlushRenderer(true);

        float width = textureX1 - textureX0, height = textureY1 - textureY0, fx = -originX, fy = -originY, fx2 = width - originX,
            fy2 = height - originY;

        bool flipX = false, flipY = false;

        if (scaleX != 1 || scaleY != 1)
        {
            flipX = scaleX < 0;
            flipY = scaleY < 0;

            float absScaleX = flipX ? -scaleX : scaleX, absScaleY = flipY ? -scaleY : scaleY;
            fx *= absScaleX;
            fy *= absScaleY;
            fx2 *= absScaleX;
            fy2 *= absScaleY;
        }

        float p1x = fx, p1y = fy, p2x = fx, p2y = fy2, p3x = fx2, p3y = fy2, p4x = fx2, p4y = fy, x1, y1, x2, y2, x3, y3, x4, y4;
        if (rotation != 0)
        {
            var (sin, cos) = MathF.SinCos(rotation);

            x1 = cos * p1x - sin * p1y;
            y1 = sin * p1x + cos * p1y;
            x2 = cos * p2x - sin * p2y;
            y2 = sin * p2x + cos * p2y;
            x3 = cos * p3x - sin * p3y;
            y3 = sin * p3x + cos * p3y;
            x4 = x1 + (x3 - x2);
            y4 = y3 - (y2 - y1);
        }
        else
        {
            x1 = p1x;
            y1 = p1y;
            x2 = p2x;
            y2 = p2y;
            x3 = p3x;
            y3 = p3y;
            x4 = p4x;
            y4 = p4y;
        }

        QuadPrimitive spritePrimitive = new()
        {
            x1 = x1 + x, y1 = y1 + y, x2 = x2 + x, y2 = y2 + y, x3 = x3 + x, y3 = y3 + y, x4 = x4 + x, y4 = y4 + y
        };

        var textureUvBounds = texture.UvBounds;
        var textureUvRatio = texture.UvRatio;

        float textureU0 = textureUvBounds.X + textureX0 * textureUvRatio.X,
            textureV0 = textureUvBounds.Y + textureY0 * textureUvRatio.Y,
            textureU1 = textureUvBounds.X + textureX1 * textureUvRatio.X,
            textureV1 = textureUvBounds.Y + textureY1 * textureUvRatio.Y, u0, v0, u1, v1;

        if (flipX)
        {
            u0 = textureU1;
            u1 = textureU0;
        }
        else
        {
            u0 = textureU0;
            u1 = textureU1;
        }

        if (flipY)
        {
            v0 = textureV1;
            v1 = textureV0;
        }
        else
        {
            v0 = textureV0;
            v1 = textureV1;
        }

        spritePrimitive.u1 = u0;
        spritePrimitive.v1 = v0;
        spritePrimitive.u2 = u0;
        spritePrimitive.v2 = v1;
        spritePrimitive.u3 = u1;
        spritePrimitive.v3 = v1;
        spritePrimitive.u4 = u1;
        spritePrimitive.v4 = v0;
        spritePrimitive.color1 = spritePrimitive.color2 = spritePrimitive.color3 = spritePrimitive.color4 = color.ToRgba();

        Unsafe.Write(Unsafe.Add<QuadPrimitive>(primitives, spritesInBatch), spritePrimitive);

        ++RenderedSpriteCount;
        ++spritesInBatch;
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #region Default Shader

    public static Shader CreateDefaultShader()
    {
        ShaderBuilder sb = new(VertexDeclaration);

        var combinedMatrix = sb.AddUniform(CombinedMatrixUniformName, "mat4");
        var texture = sb.AddUniform(TextureUniformName, "sampler2D");

        var color = sb.AddVarying("vec4");
        var textureCoord = sb.AddVarying("vec2");

        sb.VertexShader = new Sequence(new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(textureCoord, sb.VertexDeclaration.GetAttribute(AttributeUsage.DiffuseMapCoord)), new Assign(sb.GlPosition,
                () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name
                }, 0, 1)"));

        sb.FragmentShader =
            new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref} * texture2D({texture.Ref}, {textureCoord.Ref})"));

        return sb.Build();
    }

    #endregion

    ~SpriteRendererBuffered() => Dispose(false);
    public void Dispose(bool disposing)
    {
        if (disposed) return;
        if (rendering) EndRendering();

        NativeMemory.Free(primitives);
        if (!disposing) return;

        primitives = null;
        camera = null;

        primitiveStreamer.Dispose();
        primitiveStreamer = null;

        if (ownsShader) shader.Dispose();
        shader = null;

        FlushAction = null;
        disposed = true;
    }
}