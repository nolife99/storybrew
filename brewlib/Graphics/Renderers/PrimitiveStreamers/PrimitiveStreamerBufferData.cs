namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;

sealed class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    readonly nint primitiveBuffer = Marshal.AllocHGlobal(Unsafe.SizeOf<TPrimitive>() * maxPrimitivesPerBatch);
    int primitiveBufferOffset;

    protected override void internalAddPrimitive(scoped ref readonly TPrimitive primitive)
        => Unsafe.Add(ref primitiveBuffer.AsRef<TPrimitive>(), primitiveBufferOffset++) = primitive;

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        GL.BufferSubData(BufferTarget.ArrayBuffer, 0, totalQueuedPrimitives * PrimitiveSize, primitiveBuffer);

        primitiveBufferOffset = 0;

        if (IndexBufferId != -1) GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, 0, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, 0, queuedRenders, 0);
    }

    protected override void internalBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

    protected override void initializeVertexBuffer()
    {
        base.initializeVertexBuffer();
        GL.BufferStorage(BufferTarget.ArrayBuffer,
            MaxPrimitivesPerBatch * PrimitiveSize,
            0,
            BufferStorageFlags.DynamicStorageBit);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Marshal.FreeHGlobal(primitiveBuffer);
    }
}