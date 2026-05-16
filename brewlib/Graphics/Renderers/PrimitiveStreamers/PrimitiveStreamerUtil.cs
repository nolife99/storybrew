namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using BrewLib.Graphics.Shaders;

static class PrimitiveStreamerUtil
{
    public static IPrimitiveStreamer<TPrimitive> DefaultCreatePrimitiveStreamer<TPrimitive>(VertexDeclaration vertDec,
        int minVert,
        scoped ReadOnlySpan<ushort> indices) where TPrimitive : unmanaged
        => PrimitiveStreamerBufferData<TPrimitive>.HasCapabilities() ?
            new PrimitiveStreamerBufferData<TPrimitive>(vertDec, minVert, indices) :
            throw new NotSupportedException();
}
