﻿using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers.PrimitiveStreamers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Shaders.Snippets;
using osuTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using BrewLib.Util;

namespace BrewLib.Graphics.Renderers;

public class LineRendererBuffered : LineRenderer
{
    public const int VertexPerLine = 2;
    public const string CombinedMatrixUniformName = "u_combinedMatrix";

    public static readonly VertexDeclaration VertexDeclaration = new(VertexAttribute.CreatePosition3d(), VertexAttribute.CreateColor(true));

    public Action FlushAction;

    #region Default Shader

    public static Shader CreateDefaultShader()
    {
        var sb = new ShaderBuilder(VertexDeclaration);

        var combinedMatrix = sb.AddUniform(CombinedMatrixUniformName, "mat4");
        var color = sb.AddVarying("vec4");

        sb.VertexShader = new Sequence(
            new Assign(color, sb.VertexDeclaration.GetAttribute(AttributeUsage.Color)),
            new Assign(sb.GlPosition, () => $"{combinedMatrix.Ref} * vec4({sb.VertexDeclaration.GetAttribute(AttributeUsage.Position).Name}, 1)")
        );
        sb.FragmentShader = new Sequence(new Assign(sb.GlFragColor, () => $"{color.Ref}"));

        return sb.Build();
    }

    #endregion

    Shader shader;
    readonly int combinedMatrixLocation;

    public Shader Shader => ownsShader ? null : shader;
    readonly bool ownsShader;

    PrimitiveStreamer<LinePrimitive> primitiveStreamer;
    LinePrimitive[] primitives;

    Camera camera;
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

    Matrix4x4 transformMatrix = Matrix4x4.Identity;
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

    int linesInBatch, currentLargestBatch;
    readonly int maxLinesPerBatch;
    bool rendering;

    public int RenderedLineCount { get; set; }
    public int FlushedBufferCount { get; set; }
    public int DiscardedBufferCount => primitiveStreamer.DiscardedBufferCount;
    public int BufferWaitCount => primitiveStreamer.BufferWaitCount;
    public int LargestBatch { get; set; }

    public LineRendererBuffered(Shader shader = null, int maxLinesPerBatch = 4096, int primitiveBufferSize = 0)
        : this(PrimitiveStreamerUtil<LinePrimitive>.DefaultCreatePrimitiveStreamer, shader, maxLinesPerBatch, primitiveBufferSize) { }

    public LineRendererBuffered(Func<VertexDeclaration, int, PrimitiveStreamer<LinePrimitive>> createPrimitiveStreamer, Shader shader = null, int maxLinesPerBatch = 4096, int primitiveBufferSize = 0)
    {
        if (shader is null)
        {
            shader = CreateDefaultShader();
            ownsShader = true;
        }

        this.maxLinesPerBatch = maxLinesPerBatch;
        this.shader = shader;

        combinedMatrixLocation = shader.GetUniformLocation(CombinedMatrixUniformName);

        var primitiveBatchSize = Math.Max(maxLinesPerBatch, primitiveBufferSize / (VertexPerLine * VertexDeclaration.VertexSize));
        primitiveStreamer = createPrimitiveStreamer(VertexDeclaration, primitiveBatchSize * VertexPerLine);

        primitives = ArrayPool<LinePrimitive>.Shared.Rent(maxLinesPerBatch);
        Trace.WriteLine($"Initialized {nameof(LineRenderer)} using {primitiveStreamer.GetType().Name}");
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    public void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (rendering) EndRendering();

        ArrayPool<LinePrimitive>.Shared.Return(primitives);
        primitives = null;

        primitiveStreamer.Dispose();
        primitiveStreamer = null;

        if (ownsShader) shader.Dispose();
        shader = null;
    }
    public void BeginRendering()
    {
        if (rendering) throw new InvalidOperationException("Already rendering");

        shader.Begin();
        primitiveStreamer.Bind(shader);

        rendering = true;
    }
    public void EndRendering()
    {
        if (!rendering) throw new InvalidOperationException("Not rendering");

        primitiveStreamer.Unbind();
        shader.End();

        rendering = false;
    }

    bool lastFlushWasBuffered;
    public void Flush(bool canBuffer = false)
    {
        if (linesInBatch == 0) return;

        if (!lastFlushWasBuffered)
        {
            var combinedMatrix = transformMatrix * Camera.ProjectionView;
            unsafe
            {
                GL.UniformMatrix4(shader.GetUniformLocation(CombinedMatrixUniformName), 1, false, &combinedMatrix.M11);
            }

            FlushAction?.Invoke();
        }
        primitiveStreamer.Render(PrimitiveType.Lines, primitives, linesInBatch, linesInBatch * VertexPerLine, canBuffer);

        currentLargestBatch += linesInBatch;
        if (!canBuffer)
        {
            LargestBatch = Math.Max(LargestBatch, currentLargestBatch);
            currentLargestBatch = 0;
        }

        linesInBatch = 0;
        ++FlushedBufferCount;

        lastFlushWasBuffered = canBuffer;
    }

    public void Draw(Vector3 start, Vector3 end, Color color) => Draw(start, end, color, color);
    public void Draw(Vector3 start, Vector3 end, Color startColor, Color endColor)
    {
        if (!rendering) throw new InvalidOperationException("Not rendering");
        if (linesInBatch == maxLinesPerBatch) DrawState.FlushRenderer(true);

        LinePrimitive linePrimitive = new()
        {
            x1 = start.X,
            y1 = start.Y,
            z1 = start.Z,
            color1 = startColor.ToRgba(),

            x2 = end.X,
            y2 = end.Y,
            z2 = end.Z,
            color2 = endColor.ToRgba()
        };

        primitives[linesInBatch] = linePrimitive;

        ++RenderedLineCount;
        ++linesInBatch;
    }
}