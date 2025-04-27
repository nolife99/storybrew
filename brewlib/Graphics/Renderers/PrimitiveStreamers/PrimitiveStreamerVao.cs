namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Memory;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shaders;

public abstract class PrimitiveStreamerVao<TPrimitive> : IPrimitiveStreamer<TPrimitive>
    where TPrimitive : struct, allows ref struct
{
    readonly UnmanagedList<nint> drawOffsets;

    readonly UnmanagedList<int> multiDrawQueue = new(), firsts;
    bool Bound;

    protected int totalQueuedPrimitives, primitivesInBatch;

    int vertexArrayId = -1;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int minRenderableVertexCount,
        ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");
        if (!indices.IsEmpty && minRenderableVertexCount > ushort.MaxValue)
            throw new ArgumentException("Can't have more than " + ushort.MaxValue + " indexed vertices");

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

    public void AddPrimitive(ref readonly TPrimitive primitive)
    {
        if (primitivesInBatch == MinRenderableVertexCount) DrawState.FlushRenderer(true);
        AddPrimitiveInternal(in primitive);

        ++primitivesInBatch;
        ++totalQueuedPrimitives;
    }

    public void Bind(Shader shader)
    {
        if (Bound || shader is null) return;

        if (CurrentShader != shader) setupVertexArray(shader);
        GL.BindVertexArray(vertexArrayId);

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

        RenderInternal(type,
            totalQueuedPrimitives,
            multiDrawQueue.GetSpan(),
            usesIndex ? drawOffsets.GetSpan() : default,
            usesIndex ? default : firsts.GetSpan());

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

        var drawOffset = (totalQueuedPrimitives - primitivesInBatch) * sizeof(ushort) * vertexCount;
        if (IndexBufferId != -1) drawOffsets.Add(drawOffset);
        else firsts.Add(drawOffset);

        primitivesInBatch = 0;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract void AddPrimitiveInternal(ref readonly TPrimitive primitive);

    protected abstract void RenderInternal(PrimitiveType type,
        int primitiveCount,
        ReadOnlySpan<int> counts,
        ReadOnlySpan<nint> indices,
        ReadOnlySpan<int> firsts);

    protected virtual void initializeVertexBuffer()
    {
        GL.CreateBuffers(1, out int buffer);
        VertexBufferId = buffer;
    }

    void initializeIndexBuffer(ReadOnlySpan<ushort> indices)
    {
        GL.CreateBuffers(1, out int buffer);
        IndexBufferId = buffer;

        GL.NamedBufferStorage(IndexBufferId,
            indices.Length * sizeof(ushort),
            ref MemoryMarshal.GetReference(indices),
            BufferStorageFlags.None);
    }

    void setupVertexArray(Shader shader)
    {
        var initial = CurrentShader is null;
        if (initial) GL.CreateVertexArrays(1, out vertexArrayId);
        else VertexDeclaration.DeactivateAttributes(CurrentShader, vertexArrayId);

        VertexDeclaration.ActivateAttributes(shader, vertexArrayId);
        GL.VertexArrayVertexBuffer(vertexArrayId, 0, VertexBufferId, 0, VertexDeclaration.VertexSize);

        if (initial && IndexBufferId != -1) GL.VertexArrayElementBuffer(vertexArrayId, IndexBufferId);

        CurrentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        Unbind();

        if (vertexArrayId != -1) GL.DeleteVertexArray(vertexArrayId);
        if (VertexBufferId != -1) GL.DeleteBuffer(VertexBufferId);
        if (IndexBufferId != -1) GL.DeleteBuffer(IndexBufferId);

        ((IDisposable)multiDrawQueue).Dispose();
        ((IDisposable)firsts)?.Dispose();
        ((IDisposable)drawOffsets)?.Dispose();
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_vertex_array_object") &&
        GLFW.ExtensionSupported("GL_ARB_buffer_storage");
}