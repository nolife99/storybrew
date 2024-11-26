﻿namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices) where TPrimitive : unmanaged
{
    readonly GpuCommandSync commandSync = new();
    int bufferOffset, drawOffset, vertexBufferSize;
    nint bufferPointer;

    public override unsafe void Render(PrimitiveType type, nint primitives, int count, int drawCount)
    {
        var vertexDataSize = count * PrimitiveSize;
        if (bufferOffset + vertexDataSize > vertexBufferSize)
        {
            bufferOffset = 0;
            drawOffset = 0;
        }

        if (commandSync.WaitForRange(bufferOffset, vertexDataSize))
        {
            ++BufferWaitCount;
            expandVertexBuffer();
        }

        NativeMemory.Copy((void*)primitives, (byte*)bufferPointer + bufferOffset, (nuint)vertexDataSize);

        if (IndexBufferId != -1) GL.DrawElements(type, drawCount, DrawElementsType.UnsignedShort, drawOffset * sizeof(ushort));
        else GL.DrawArrays(type, drawOffset, drawCount);

        commandSync.LockRange(bufferOffset, vertexDataSize);

        bufferOffset += vertexDataSize;
        drawOffset += drawCount;
    }
    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

        GL.BufferStorage(BufferTarget.ArrayBuffer, vertexBufferSize, 0,
            BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit);

        bufferPointer = GL.MapBufferRange(BufferTarget.ArrayBuffer, 0, vertexBufferSize,
            BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit |
            BufferAccessMask.MapUnsynchronizedBit | BufferAccessMask.MapInvalidateBufferBit);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
    protected override void Dispose(bool disposing)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.UnmapBuffer(BufferTarget.ArrayBuffer);

        if (disposing) commandSync.Dispose();
        base.Dispose(disposing);
    }
    void expandVertexBuffer()
    {
        // Prevent the vertex buffer from becoming too large (maxes at 4mb * grow factor)
        if (IndexBufferId != -1 || MinRenderableVertexCount * VertexDeclaration.VertexSize > 4194304) return;

        MinRenderableVertexCount = (int)(MinRenderableVertexCount * 1.75f);
        if (commandSync.WaitForAll()) ++BufferWaitCount;

        Unbind();

        // Rebuild the VBO

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.UnmapBuffer(BufferTarget.ArrayBuffer);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.DeleteBuffer(VertexBufferId);

        initializeVertexBuffer();

        // Rebuild the VAO

        Bind(CurrentShader);
        CurrentShader = null;

        bufferOffset = 0;
        drawOffset = 0;

        ++DiscardedBufferCount;
    }

    public new static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_buffer_storage") &&
        GLFW.ExtensionSupported("GL_ARB_map_buffer_range") && GpuCommandSync.HasCapabilities() &&
        PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}