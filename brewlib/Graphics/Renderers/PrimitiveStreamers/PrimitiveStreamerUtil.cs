namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;

public static class PrimitiveStreamerUtil<TPrimitive> where TPrimitive : unmanaged
{
    public static PrimitiveStreamer DefaultCreatePrimitiveStreamer(VertexDeclaration vertDec,
        int minVert,
        ReadOnlySpan<ushort> indices)
    {
        if (PrimitiveStreamerPersistentMap<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerPersistentMap<TPrimitive>(vertDec, minVert, indices);

        if (PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert, indices);

        if (PrimitiveStreamerVbo<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerVbo<TPrimitive>(vertDec, indices);

        throw new NotSupportedException();
    }
}