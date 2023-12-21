using System;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

public static class PrimitiveStreamerUtil<TPrimitive> where TPrimitive : unmanaged
{
    public static readonly Func<VertexDeclaration, int, PrimitiveStreamer<TPrimitive>> DefaultCreatePrimitiveStreamer = (vertDec, minVert) =>
    {
        if (PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities()) return new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert);
        if (PrimitiveStreamerPersistentMap<TPrimitive>.HasCapabilities()) return new PrimitiveStreamerPersistentMap<TPrimitive>(vertDec, minVert);
        if (PrimitiveStreamerVbo<TPrimitive>.HasCapabilities()) return new PrimitiveStreamerVbo<TPrimitive>(vertDec);

        throw new NotSupportedException();
    };
}