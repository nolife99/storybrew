namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using OpenTK.Graphics.OpenGL;

sealed class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    readonly TPrimitive[] primitiveBuffer = GC.AllocateUninitializedArray<TPrimitive>(maxPrimitivesPerBatch);

    protected override void internalAddPrimitive(scoped ref readonly TPrimitive primitive)
        => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(primitiveBuffer), totalQueuedPrimitives) = primitive;

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        GL.BufferData(BufferTarget.ArrayBuffer,
            totalQueuedPrimitives * PrimitiveSize,
            primitiveBuffer,
            BufferUsageHint.StaticDraw);

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, commandBufferOffset, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, commandBufferOffset, queuedRenders, 0);

        if (DrawState.CanInvalidate) GL.InvalidateBufferData(VertexBufferId);
    }

    protected override void internalBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);

    public new static bool HasCapabilities() => DrawState.Extensions.Contains("GL_ARB_multi_draw_indirect");
}