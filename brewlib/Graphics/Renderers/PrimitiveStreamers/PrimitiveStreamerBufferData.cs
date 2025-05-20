namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Collections.Pooled;
using OpenTK.Graphics.OpenGL;
using Shaders;

internal sealed class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    ReadOnlySpan<ushort> indices) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices)
    where TPrimitive : struct
{
    readonly PooledList<TPrimitive> primitiveBuffer = new();

    protected override void internalAddPrimitive(ref readonly TPrimitive primitive) => primitiveBuffer.Add(primitive);

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        GL.BufferSubData(BufferTarget.ArrayBuffer, 0, totalQueuedPrimitives * PrimitiveSize, ref MemoryMarshal.GetReference(primitiveBuffer.Span));
        primitiveBuffer.Clear();

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandPtrOffset, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandPtrOffset, queuedRenders, 0);
    }

    protected override void internalBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        GL.BufferStorage(BufferTarget.ArrayBuffer, MaxPrimitivesPerBatch * PrimitiveSize, 0, BufferStorageFlags.DynamicStorageBit);
    }
}