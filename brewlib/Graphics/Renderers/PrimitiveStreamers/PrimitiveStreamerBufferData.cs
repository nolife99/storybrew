namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using Shaders;

public class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int minRenderableVertexCount,
    ReadOnlySpan<ushort> indices) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indices)
    where TPrimitive : struct, allows ref struct
{
    byte[] primitiveBuffer;

    protected override void internalAddPrimitive(ref readonly TPrimitive primitive) => Unsafe.Add(
        ref Unsafe.As<byte, TPrimitive>(ref MemoryMarshal.GetArrayDataReference(primitiveBuffer)),
        totalQueuedPrimitives) = primitive;

    protected override void internalRender(PrimitiveType type, int vertexCount,
        ReadOnlySpan<int> counts,
        ReadOnlySpan<nint> indices,
        ReadOnlySpan<int> firsts)
    {
        var vertexDataSize = totalQueuedPrimitives * PrimitiveSize;
        GL.BufferSubData(BufferTarget.ArrayBuffer, 0, vertexDataSize, primitiveBuffer);

        if (IndexBufferId != -1)
            GL.MultiDrawElements(type,
                ref MemoryMarshal.GetReference(counts),
                DrawElementsType.UnsignedShort,
                ref MemoryMarshal.GetReference(indices),
                counts.Length);
        else
            GL.MultiDrawArrays(type,
                ref MemoryMarshal.GetReference(firsts),
                ref MemoryMarshal.GetReference(counts),
                counts.Length);
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