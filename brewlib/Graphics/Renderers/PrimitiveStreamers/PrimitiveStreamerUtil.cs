namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using Shaders;

public static class PrimitiveStreamerUtil
{
    public static IPrimitiveStreamer<TPrimitive> DefaultCreatePrimitiveStreamer<TPrimitive>(VertexDeclaration vertDec,
        int minVert,
        ReadOnlySpan<ushort> indices) where TPrimitive : struct, allows ref struct
    {
        if (PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert, indices);

        if (PrimitiveStreamerPersistentMap<TPrimitive>.HasCapabilities())
            return new PrimitiveStreamerPersistentMap<TPrimitive>(vertDec, minVert, indices);

        throw new NotSupportedException();
    }
}