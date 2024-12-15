namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shaders;

public abstract class PrimitiveStreamerVao<TPrimitive> : PrimitiveStreamer<TPrimitive> where TPrimitive : allows ref struct
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
        PrimitiveSize = Unsafe.SizeOf<TPrimitive>();

        initializeVertexBuffer();
        if (!indices.IsEmpty) initializeIndexBuffer(indices);
    }

    public abstract ref TPrimitive PrimitiveAt(int index);

    public void Bind(Shader shader)
    {
        if (Bound || shader is null) return;

        if (CurrentShader != shader) setupVertexArray(shader);
        GL.BindVertexArray(VertexArrayId);

        Bound = true;
    }
    public void Unbind()
    {
        if (!Bound) return;

        GL.BindVertexArray(0);
        Bound = false;
    }

    public abstract void Render(PrimitiveType type, int primitiveCount, int vertices);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void initializeVertexBuffer() => GL.CreateBuffers(1, out VertexBufferId);
    void initializeIndexBuffer(ReadOnlySpan<ushort> indices)
    {
        GL.CreateBuffers(1, out IndexBufferId);
        GL.NamedBufferStorage(IndexBufferId,
            indices.Length * sizeof(ushort),
            ref MemoryMarshal.GetReference(indices),
            BufferStorageFlags.None);
    }

    void setupVertexArray(Shader shader)
    {
        var initial = CurrentShader is null;
        if (initial) GL.CreateVertexArrays(1, out VertexArrayId);

        GL.VertexArrayVertexBuffer(VertexArrayId, 0, VertexBufferId, 0, VertexDeclaration.VertexSize);
        if (!initial) VertexDeclaration.DeactivateAttributes(CurrentShader, VertexArrayId);
        VertexDeclaration.ActivateAttributes(shader, VertexArrayId);

        if (initial && IndexBufferId != -1) GL.VertexArrayElementBuffer(VertexArrayId, IndexBufferId);

        CurrentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);
    protected virtual void Dispose(bool disposing)
    {
        Unbind();

        if (VertexArrayId != -1) GL.DeleteVertexArray(VertexArrayId);
        if (VertexBufferId != -1) GL.DeleteBuffer(VertexBufferId);
        if (IndexBufferId != -1) GL.DeleteBuffer(IndexBufferId);
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_vertex_array_object") &&
        GLFW.ExtensionSupported("GL_ARB_buffer_storage");
}