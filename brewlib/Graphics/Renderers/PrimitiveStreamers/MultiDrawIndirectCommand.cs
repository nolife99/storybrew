namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal ref struct MultiDrawArraysIndirectCommand
{
    public uint Count, InstanceCount, FirstVertex, BaseInstance;
}

[StructLayout(LayoutKind.Sequential, Size = 20)]
internal ref struct MultiDrawElementsIndirectCommand
{
    public uint Count, InstanceCount, FirstIndex;
    public int BaseVertex;
    public uint BaseInstance;
}