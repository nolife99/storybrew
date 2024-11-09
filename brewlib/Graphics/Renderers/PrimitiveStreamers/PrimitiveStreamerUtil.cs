namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;

public static class PrimitiveStreamerUtil<TPrimitive> where TPrimitive : unmanaged
{
    public static PrimitiveStreamer DefaultCreatePrimitiveStreamer(VertexDeclaration vertDec, int minVert)
    {
        if (PrimitiveStreamerPersistentMap<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerPersistentMap<TPrimitive>(vertDec, minVert);

        if (PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert);

        if (PrimitiveStreamerVbo<TPrimitive>.HasCapabilities()) return new PrimitiveStreamerVbo<TPrimitive>(vertDec);

        throw new NotSupportedException();
    }
}