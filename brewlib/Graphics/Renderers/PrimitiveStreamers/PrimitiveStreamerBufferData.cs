namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using Shaders;

internal sealed class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices)
    where TPrimitive : struct, allows ref struct
{
    byte[] primitiveBuffer;

    protected override void internalAddPrimitive(ref readonly TPrimitive primitive) => Unsafe.Add(
        ref Unsafe.As<byte, TPrimitive>(ref MemoryMarshal.GetArrayDataReference(primitiveBuffer)),
        totalQueuedPrimitives) = primitive;

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        var vertexDataSize = totalQueuedPrimitives * PrimitiveSize;
        GL.BufferSubData(BufferTarget.ArrayBuffer, 0, vertexDataSize, primitiveBuffer);

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandPtrOffset, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandPtrOffset, queuedRenders, 0);
    }

    protected override void internalBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();

        var vertexBufferSize = MinRenderableVertexCount * PrimitiveSize;

        primitiveBuffer = GC.AllocateUninitializedArray<byte>(vertexBufferSize);
        GL.BufferStorage(BufferTarget.ArrayBuffer, vertexBufferSize, 0, BufferStorageFlags.DynamicStorageBit);
    }
}