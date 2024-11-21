namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;

public static class PrimitiveStreamerUtil
{
    public static PrimitiveStreamer DefaultCreatePrimitiveStreamer<TPrimitive>(VertexDeclaration vertDec,
        int minVert,
        ReadOnlySpan<ushort> indices) where TPrimitive : unmanaged
    {
        if (PrimitiveStreamerPersistentMap<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerPersistentMap<TPrimitive>(vertDec, minVert, indices);

        if (PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert, indices);

        throw new NotSupportedException();
    }
}