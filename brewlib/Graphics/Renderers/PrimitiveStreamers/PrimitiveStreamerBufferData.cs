namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;

sealed class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    readonly nint primitiveBuffer = Marshal.AllocHGlobal(maxPrimitivesPerBatch * PrimitiveSize);
    int primitiveBufferOffset;

    protected override void internalAddPrimitive(scoped ref readonly TPrimitive primitive)
        => (primitiveBuffer + primitiveBufferOffset++ * PrimitiveSize).AsRef<TPrimitive>() = primitive;

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

    protected override void Dispose(bool disposing)
    {
        Marshal.FreeHGlobal(primitiveBuffer);
        base.Dispose(disposing);
    }

    public new static bool HasCapabilities() => DrawState.Extensions.Contains("GL_ARB_multi_draw_indirect");
}