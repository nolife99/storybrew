using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Util;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration, int minRenderableVertexCount, ushort[] indexes = null) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indexes), PrimitiveStreamer<TPrimitive> where TPrimitive : struct
{
    GpuCommandSync commandSync = new();
    nint bufferPointer;
    int bufferOffset, drawOffset, vertexBufferSize;

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;

        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.BufferStorage(BufferTarget.ArrayBuffer, vertexBufferSize, 0, BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit);
        bufferPointer = GL.MapBufferRange(BufferTarget.ArrayBuffer, 0, vertexBufferSize, BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit);

        DrawState.CheckError("mapping vertex buffer", bufferPointer == 0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }
    protected override void internalDispose()
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
        GL.UnmapBuffer(BufferTarget.ArrayBuffer);

        commandSync.Dispose();
        commandSync = null;

        base.internalDispose();
    }
    public override void Render(PrimitiveType primitiveType, TPrimitive[] primitives, int primitiveCount, int drawCount, bool canBuffer = false)
    {
        if (!Bound) throw new InvalidOperationException("Not bound");

        var vertexDataSize = primitiveCount * PrimitiveSize;
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

        unsafe
        {
            fixed (void* pinned = &Unsafe.As<TPrimitive, byte>(ref MemoryMarshal.GetArrayDataReference(primitives)))
                Native.CopyMemory((nint)pinned, bufferPointer + bufferOffset, vertexDataSize);
        }

        if (IndexBufferId != -1) GL.DrawElements(primitiveType, drawCount, DrawElementsType.UnsignedShort, drawOffset << 1);
        else GL.DrawArrays(primitiveType, drawOffset, drawCount);

        commandSync.LockRange(bufferOffset, vertexDataSize);

        bufferOffset += vertexDataSize;
        drawOffset += drawCount;
    }
    void expandVertexBuffer()
    {
        if (IndexBufferId != -1) return;

        // Prevent the vertex buffer from becoming too large (maxes at 8mb * grow factor)
        if (MinRenderableVertexCount * VertexDeclaration.VertexSize > 8388608) return;

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

        GL.BindVertexArray(0);
        GL.DeleteVertexArray(VertexArrayId);

        var previousShader = CurrentShader;
        CurrentShader = null;
        Bind(previousShader);

        bufferOffset = 0;
        drawOffset = 0;

        ++DiscardedBufferCount;
    }
    public new static bool HasCapabilities() => DrawState.HasCapabilities(4, 4, "GL_ARB_buffer_storage") &&
        DrawState.HasCapabilities(3, 0, "GL_ARB_map_buffer_range") && DrawState.HasCapabilities(1, 5) && GpuCommandSync.HasCapabilities() &&
        PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
}