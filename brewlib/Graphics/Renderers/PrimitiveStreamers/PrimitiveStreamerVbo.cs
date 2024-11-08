namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using osuTK.Graphics.OpenGL;

public class PrimitiveStreamerVbo<TPrimitive> : PrimitiveStreamer where TPrimitive : unmanaged
{
    readonly int primitiveSize;
    readonly VertexDeclaration vertexDeclaration;
    bool bound;

    Shader currentShader;

    bool disposed;

    int vertexBufferId = -1, indexBufferId = -1;

    public PrimitiveStreamerVbo(VertexDeclaration vertexDeclaration, ushort[] indexes = null)
    {
        if (vertexDeclaration.AttributeCount < 1)
            throw new ArgumentException("At least one vertex attribute is required");

        this.vertexDeclaration = vertexDeclaration;
        primitiveSize = Marshal.SizeOf<TPrimitive>();

        initializeVertexBuffer();
        if (indexes is not null) initializeIndexBuffer(indexes);
    }

    public int DiscardedBufferCount { get; protected set; }
    public int BufferWaitCount { get; protected set; }

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

    public unsafe void Render(PrimitiveType primitiveType, void* primitives, int primitiveCount, int drawCount,
        bool canBuffer = false)
    {
        GL.BufferData(BufferTarget.ArrayBuffer, primitiveCount * primitiveSize, (nint)primitives,
            BufferUsageHint.StaticDraw);
        ++DiscardedBufferCount;

        if (indexBufferId != -1)
            GL.DrawElements(primitiveType, drawCount, DrawElementsType.UnsignedShort, 0);
        else
            GL.DrawArrays(primitiveType, 0, drawCount);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void initializeVertexBuffer() => vertexBufferId = GL.GenBuffer();

    protected virtual void initializeIndexBuffer(ushort[] indexes)
    {
        indexBufferId = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indexes.Length * sizeof(ushort), indexes,
            BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    protected virtual void internalBind(Shader shader)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);

        vertexDeclaration.ActivateAttributes(shader);
        currentShader = shader;
    }

    protected virtual void internalUnbind()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);

        vertexDeclaration.DeactivateAttributes(currentShader);
        currentShader = null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

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

        if (disposing)
        {
            vertexBufferId = -1;
            indexBufferId = -1;
            disposed = true;
        }
    }

    ~PrimitiveStreamerVbo() => Dispose(false);

    public static bool HasCapabilities() => DrawState.HasCapabilities(2, 0);
}