namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)] ref struct MultiDrawArraysIndirectCommand
{
    public uint Count, InstanceCount, FirstVertex, BaseInstance;
}

[StructLayout(LayoutKind.Sequential)] ref struct MultiDrawElementsIndirectCommand
{
    public uint Count, InstanceCount, FirstIndex;
    public int BaseVertex;
    public uint BaseInstance;
}