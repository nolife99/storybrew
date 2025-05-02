namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Collections.Pooled;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shaders;

public abstract class PrimitiveStreamerVao<TPrimitive> : IPrimitiveStreamer<TPrimitive>
    where TPrimitive : struct, allows ref struct
{
    readonly PooledList<nint> drawOffsets;

    readonly PooledList<int> multiDrawQueue = new(), firsts;
    bool Bound;

    protected int totalQueuedPrimitives, primitivesInBatch;

    int vertexArrayId = -1;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int minRenderableVertexCount,
        ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");

        MinRenderableVertexCount = minRenderableVertexCount;
        VertexDeclaration = vertexDeclaration;
        PrimitiveSize = Unsafe.SizeOf<TPrimitive>();

        initializeVertexBuffer();

        if (indices.IsEmpty) firsts = new();
        else
        {
            initializeIndexBuffer(indices);
            drawOffsets = new();
        }
    }

    protected Shader CurrentShader { get; set; }
    protected int VertexBufferId { get; private set; } = -1;
    protected int IndexBufferId { get; private set; } = -1;
    protected int PrimitiveSize { get; }
    protected int MinRenderableVertexCount { get; set; }
    protected VertexDeclaration VertexDeclaration { get; }

    public void AddPrimitive(ref readonly TPrimitive primitive, int vertexCount)
    {
        if (totalQueuedPrimitives == MinRenderableVertexCount / vertexCount) DrawState.FlushRenderer(true);
        AddPrimitiveInternal(in primitive);

        ++primitivesInBatch;
        ++totalQueuedPrimitives;
    }

    public void Bind(Shader shader)
    {
        if (Bound || shader is null) return;

        if (CurrentShader != shader) setupVertexArray(shader);
        GL.BindVertexArray(vertexArrayId);

        internalBind();

        Bound = true;
    }

    public void Unbind()
    {
        if (!Bound) return;

        GL.BindVertexArray(0);
        Bound = false;
    }

    public void Render(PrimitiveType type)
    {
        var usesIndex = IndexBufferId != -1;

        RenderInternal(type, multiDrawQueue.Span, usesIndex ? drawOffsets.Span : default, usesIndex ? default : firsts.Span);

        multiDrawQueue.Clear();
        if (IndexBufferId != -1) drawOffsets.Clear();
        else firsts.Clear();

        totalQueuedPrimitives = 0;
    }

    public int QueuedRenders => multiDrawQueue.Count;
    public int PrimitivesInBatch => primitivesInBatch;

    public void QueueRender(int vertexCount)
    {
        multiDrawQueue.Add(primitivesInBatch * vertexCount);

        if (IndexBufferId != -1) drawOffsets.Add((totalQueuedPrimitives - primitivesInBatch) * sizeof(ushort) * vertexCount);
        else firsts.Add((totalQueuedPrimitives - primitivesInBatch) * vertexCount);

        primitivesInBatch = 0;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void internalBind() { }

    protected abstract void AddPrimitiveInternal(ref readonly TPrimitive primitive);

    protected abstract void RenderInternal(PrimitiveType type,
        ReadOnlySpan<int> counts,
        ReadOnlySpan<nint> indices,
        ReadOnlySpan<int> firsts);

    protected virtual void initializeVertexBuffer()
        => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId = GL.GenBuffer());

    void initializeIndexBuffer(ReadOnlySpan<ushort> indices)
    {
        IndexBufferId = GL.GenBuffer();

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);
        GL.BufferStorage(BufferTarget.ElementArrayBuffer,
            indices.Length * sizeof(ushort),
            ref MemoryMarshal.GetReference(indices),
            BufferStorageFlags.None);
    }

    void setupVertexArray(Shader shader)
    {
        var initial = CurrentShader is null;
        if (initial) vertexArrayId = GL.GenVertexArray();

        GL.BindVertexArray(vertexArrayId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

        if (!initial) VertexDeclaration.DeactivateAttributes(CurrentShader);
        VertexDeclaration.ActivateAttributes(shader);

        if (initial && IndexBufferId != -1) GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);

        CurrentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        Unbind();

        if (vertexArrayId != -1) GL.DeleteVertexArray(vertexArrayId);
        if (VertexBufferId != -1) GL.DeleteBuffer(VertexBufferId);
        if (IndexBufferId != -1) GL.DeleteBuffer(IndexBufferId);

        multiDrawQueue.Dispose();
        if (IndexBufferId != -1) drawOffsets.Dispose();
        else firsts.Dispose();
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_buffer_storage") &&
        GLFW.ExtensionSupported("GL_ARB_shader_storage_buffer_object");
}