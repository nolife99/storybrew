namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

public abstract class PrimitiveStreamerVao<TPrimitive> : PrimitiveStreamer<TPrimitive> where TPrimitive : unmanaged
{
    bool Bound;
    protected Shader CurrentShader;
    protected int VertexArrayId = -1, VertexBufferId = -1, IndexBufferId = -1, PrimitiveSize, MinRenderableVertexCount;
    protected VertexDeclaration VertexDeclaration;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int minRenderableVertexCount,
        ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");
        if (!indices.IsEmpty && minRenderableVertexCount > ushort.MaxValue)
            throw new ArgumentException("Can't have more than " + ushort.MaxValue + " indexed vertices");

        MinRenderableVertexCount = minRenderableVertexCount;
        VertexDeclaration = vertexDeclaration;
        PrimitiveSize = Marshal.SizeOf<TPrimitive>();

        initializeVertexBuffer();
        if (!indices.IsEmpty) initializeIndexBuffer(indices);
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

        GL.BindVertexArray(0);
        Bound = false;
    }
    public abstract void Render(PrimitiveType type, ReadOnlySpan<TPrimitive> primitives, int vertices);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void initializeVertexBuffer() => VertexBufferId = GL.GenBuffer();
    void initializeIndexBuffer(ReadOnlySpan<ushort> indices)
    {
        IndexBufferId = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, IndexBufferId);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(ushort), ref MemoryMarshal.GetReference(indices),
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
    protected virtual void internalBind(Shader shader)
    {
        if (CurrentShader != shader) setupVertexArray(shader);
        else GL.BindVertexArray(VertexArrayId);
    }

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
        CurrentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);
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
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_vertex_array_object");
}