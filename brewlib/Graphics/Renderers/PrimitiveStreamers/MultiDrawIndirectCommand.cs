namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
struct MultiDrawArraysIndirectCommand
{
    public uint Count, InstanceCount, FirstVertex, BaseInstance;
}

[StructLayout(LayoutKind.Sequential)]
struct MultiDrawElementsIndirectCommand
{
    public uint Count, InstanceCount, FirstIndex;
    public int BaseVertex;
    public uint BaseInstance;
}