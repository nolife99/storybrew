﻿using System;
using System.Runtime.InteropServices;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

public class PrimitiveStreamerVbo<TPrimitive> : PrimitiveStreamer<TPrimitive> where TPrimitive : unmanaged
{
    readonly VertexDeclaration vertexDeclaration;
    readonly int primitiveSize;

    int vertexBufferId = -1, indexBufferId = -1;

    Shader currentShader;
    bool bound;

    public int DiscardedBufferCount { get; protected set; }
    public int BufferWaitCount { get; protected set; }

    public PrimitiveStreamerVbo(VertexDeclaration vertexDeclaration, ushort[] indexes = null)
    {
        if (vertexDeclaration.AttributeCount < 1) throw new ArgumentException("At least one vertex attribute is required");

        this.vertexDeclaration = vertexDeclaration;
        primitiveSize = Marshal.SizeOf<TPrimitive>();

        initializeVertexBuffer();
        if (indexes is not null) initializeIndexBuffer(indexes);
    }

    protected virtual void initializeVertexBuffer() => vertexBufferId = GL.GenBuffer();
    protected virtual void initializeIndexBuffer(ushort[] indexes)
    {
        indexBufferId = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indexes.Length * sizeof(ushort), indexes, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
    public void Dispose()
    {
        dispose(true);
        GC.SuppressFinalize(this);
    }
    void dispose(bool disposing)
    {
        if (!disposing) return;
        if (bound) Unbind();

        internalDispose();
    }
    protected virtual void internalDispose()
    {
        if (vertexBufferId != -1)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(vertexBufferId);
            vertexBufferId = -1;
        }
        if (indexBufferId != -1)
        {
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.DeleteBuffer(indexBufferId);
            indexBufferId = -1;
        }
    }
    public void Bind(Shader shader)
    {
        ArgumentNullException.ThrowIfNull(shader);
        if (bound) throw new InvalidOperationException("Already bound");

        internalBind(shader);
        bound = true;
    }
    public void Unbind()
    {
        if (!bound) throw new InvalidOperationException("Not bound");

        internalUnbind();
        bound = false;
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
    public unsafe void Render(PrimitiveType primitiveType, TPrimitive* primitives, int primitiveCount, int drawCount, bool canBuffer = false)
    {
        GL.BufferData(BufferTarget.ArrayBuffer, primitiveCount * primitiveSize, (nint)primitives, BufferUsageHint.StaticDraw);
        ++DiscardedBufferCount;

        if (indexBufferId != -1) GL.DrawElements(primitiveType, drawCount, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(primitiveType, 0, drawCount);
    }
    public static bool HasCapabilities() => DrawState.HasCapabilities(2, 0);
}