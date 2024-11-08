namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using osuTK.Graphics.OpenGL;

public abstract class PrimitiveStreamerVao<TPrimitive> : PrimitiveStreamer where TPrimitive : unmanaged
{
    protected bool Bound;
    protected Shader CurrentShader;
    protected int VertexArrayId = -1, VertexBufferId = -1, IndexBufferId = -1, PrimitiveSize, MinRenderableVertexCount;
    protected VertexDeclaration VertexDeclaration;

    public PrimitiveStreamerVao(VertexDeclaration vertexDeclaration, int minRenderableVertexCount,
        ushort[] indexes = null)
    {
        if (vertexDeclaration.AttributeCount < 1)
            throw new ArgumentException("At least one vertex attribute is required");
        if (indexes is not null && minRenderableVertexCount > ushort.MaxValue)
            throw new ArgumentException("Can't have more than " + ushort.MaxValue + " indexed vertices");

        MinRenderableVertexCount = minRenderableVertexCount;
        VertexDeclaration = vertexDeclaration;
        PrimitiveSize = Marshal.SizeOf<TPrimitive>();

        initializeVertexBuffer();
        if (indexes is not null) initializeIndexBuffer(indexes);
    }

    public int DiscardedBufferCount { get; protected set; }
    public int BufferWaitCount { get; protected set; }

    public void Bind(Shader shader)
    {
        if (Bound || shader is null) return;

        internalBind(shader);
        Bound = true;
    }
    public void Unbind()
    {
        if (!Bound) return;

        internalUnbind();
        Bound = false;
    }
    public abstract unsafe void Render(PrimitiveType type, void* primitives, int count, int drawCount, bool canBuffer = false);

    protected virtual void initializeVertexBuffer() => VertexBufferId = GL.GenBuffer();
    protected virtual void initializeIndexBuffer(ushort[] indexes)
    {
        IndexBufferId = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indexes.Length * sizeof(ushort), indexes, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
    protected virtual void internalBind(Shader shader)
    {
        if (CurrentShader != shader) setupVertexArray(shader);
        GL.BindVertexArray(VertexArrayId);
    }
    protected virtual void internalUnbind() => GL.BindVertexArray(0);

    void setupVertexArray(Shader shader)
    {
        var initial = CurrentShader is null;

        if (initial) VertexArrayId = GL.GenVertexArray();
        GL.BindVertexArray(VertexArrayId);

        // Vertex

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        if (!initial) VertexDeclaration.DeactivateAttributes(CurrentShader);
        VertexDeclaration.ActivateAttributes(shader);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        // Index

        if (initial && IndexBufferId != -1) GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);
        GL.BindVertexArray(0);
        CurrentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        Unbind();
        if (VertexArrayId != -1)
        {
            GL.BindVertexArray(0);
            GL.DeleteVertexArray(VertexArrayId);
        }
        if (VertexBufferId != -1)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(VertexBufferId);
        }
        if (IndexBufferId != -1)
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.DeleteBuffer(IndexBufferId);
        }

        if (!disposing) return;
        VertexArrayId = -1;
        VertexBufferId = -1;
        IndexBufferId = -1;

        CurrentShader = null;
    }

    public static bool HasCapabilities()
        => DrawState.HasCapabilities(2, 0) && DrawState.HasCapabilities(3, 0, "GL_ARB_vertex_array_object");
}