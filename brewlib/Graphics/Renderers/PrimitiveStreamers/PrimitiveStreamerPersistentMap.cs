namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;

sealed class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration,
    int maxPrimitivesPerBatch,
    scoped ReadOnlySpan<ushort> indices)
    : PrimitiveStreamerBufferData<TPrimitive>(vertexDeclaration, maxPrimitivesPerBatch, indices)
    where TPrimitive : unmanaged
{
    public new static bool HasCapabilities()
        => false;
}
