namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using osuTK.Graphics.OpenGL;

class PrimitiveStreamerBufferData<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices) where TPrimitive : unmanaged
{
    readonly TPrimitive[] primitiveBuffer = GC.AllocateUninitializedArray<TPrimitive>(maxPrimitivesPerBatch);

    public override ref TPrimitive PrimitiveAt(int index)
        => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(primitiveBuffer), index);

    public override void Render(PrimitiveTopology topology, int primitiveCount, int verticesPerPrimitive)
    {
        if (primitiveCount == 0) return;

        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId);
        GL.BufferData(BufferTarget.ArrayBuffer,
            primitiveCount * PrimitiveSize,
            primitiveBuffer,
            BufferUsageHint.StreamDraw);

        var type = toOpenGlPrimitiveType(topology);
        if (indexBufferId != -1)
            GL.DrawElements(type, primitiveCount * verticesPerPrimitive, DrawElementsType.UnsignedShort, 0);
        else GL.DrawArrays(type, 0, primitiveCount * verticesPerPrimitive);

        DrawState.CountDrawCall();

        if (DrawState.CanInvalidate) GL.InvalidateBufferData(vertexBufferId);
    }

    public new static bool HasCapabilities()
        => PrimitiveStreamerVao<TPrimitive>.HasCapabilities();

    static PrimitiveType toOpenGlPrimitiveType(PrimitiveTopology topology)
        => topology switch
        {
            PrimitiveTopology.Points => PrimitiveType.Points,
            PrimitiveTopology.Lines => PrimitiveType.Lines,
            PrimitiveTopology.Triangles => PrimitiveType.Triangles,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
        };
}
