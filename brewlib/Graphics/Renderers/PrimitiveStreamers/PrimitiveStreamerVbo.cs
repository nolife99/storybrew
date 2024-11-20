namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;

public sealed class PrimitiveStreamerVbo<TPrimitive> : PrimitiveStreamer where TPrimitive : unmanaged
{
    readonly int primitiveSize;
    readonly VertexDeclaration vertexDeclaration;
    bool bound, disposed;
    Shader currentShader;

    int vertexBufferId = -1, indexBufferId = -1;

    public PrimitiveStreamerVbo(VertexDeclaration vertexDeclaration, ushort[] indexes = null)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");

        this.vertexDeclaration = vertexDeclaration;
        primitiveSize = Marshal.SizeOf<TPrimitive>();

        initializeVertexBuffer();
        if (indexes is not null) initializeIndexBuffer(indexes);
    }

    public int DiscardedBufferCount { get; private set; }
    public int BufferWaitCount { get; }

    public void Bind(Shader shader)
    {
        if (bound) return;

        internalBind(shader);
        bound = true;
    }
    public void Unbind()
    {
        if (!bound) return;

        internalUnbind();
        bound = false;
    }
    public void Render(PrimitiveType type, nint primitives, int count, int drawCount)
    {
        GL.BufferData(BufferTarget.ArrayBuffer, count * primitiveSize, primitives, BufferUsageHint.StaticDraw);
        ++DiscardedBufferCount;

        if (indexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(type, 0, drawCount);
    }
    public void Dispose()
    {
        if (!disposed) dispose();
        GC.SuppressFinalize(this);
        disposed = true;
    }

    void initializeVertexBuffer() => vertexBufferId = GL.GenBuffer();
    void initializeIndexBuffer(ushort[] indexes)
    {
        indexBufferId = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indexes.Length * sizeof(ushort), indexes, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
    void internalBind(Shader shader)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);

        vertexDeclaration.ActivateAttributes(shader);
        currentShader = shader;
    }
    void internalUnbind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

        vertexDeclaration.DeactivateAttributes(currentShader);
        currentShader = null;
    }

    ~PrimitiveStreamerVbo() => dispose();
    void dispose()
    {
        Unbind();
        if (vertexBufferId != -1)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(vertexBufferId);
        }

        if (indexBufferId != -1)
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.DeleteBuffer(indexBufferId);
        }
    }

    public static bool HasCapabilities() => DrawState.HasCapabilities(2, 0);
}