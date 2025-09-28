namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;

static class PrimitiveStreamerUtil
{
    public static IPrimitiveStreamer<TPrimitive> DefaultCreatePrimitiveStreamer<TPrimitive>(VertexDeclaration vertDec,
        int minVert,
        scoped ReadOnlySpan<ushort> indices) where TPrimitive : unmanaged
        => PrimitiveStreamerPersistentMap<TPrimitive>.HasCapabilities() ?
            new PrimitiveStreamerPersistentMap<TPrimitive>(vertDec, minVert, indices) :
            PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities() ?
                (IPrimitiveStreamer<TPrimitive>)new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert, indices) :
                throw new NotSupportedException();
}