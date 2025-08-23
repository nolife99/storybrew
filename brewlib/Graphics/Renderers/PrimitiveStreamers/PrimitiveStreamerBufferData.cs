namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;
using OpenTK.Graphics.OpenGL;

sealed class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    readonly TPrimitive[] primitiveBuffer = GC.AllocateUninitializedArray<TPrimitive>(maxPrimitivesPerBatch);
    int primitiveBufferOffset;

    protected override void internalAddPrimitive(scoped ref readonly TPrimitive primitive)
        => primitiveBuffer[primitiveBufferOffset++] = primitive;

    protected override void internalRender(PrimitiveType type, int vertexCount)
    {
        GL.BufferData(BufferTarget.ArrayBuffer,
            totalQueuedPrimitives * PrimitiveSize,
            primitiveBuffer,
            BufferUsageHint.StaticDraw);

        if (IndexBufferId != -1)
            GL.MultiDrawElementsIndirect(type, DrawElementsType.UnsignedShort, 0, queuedRenders, 0);
        else GL.MultiDrawArraysIndirect(type, 0, queuedRenders, 0);

        if (DrawState.CanInvalidate) GL.InvalidateBufferData(VertexBufferId);

        primitiveBufferOffset = 0;
    }

    protected override void internalBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
}