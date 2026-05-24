namespace BrewLib.Graphics.Renderers;

using System;

[Flags]
public enum PrimitiveBatchFeatures
{
    None = 0,
    PainterOrdered = 1 << 0,
    Batched = 1 << 1,
    Indexed = 1 << 2,
    Instanced = 1 << 3,
    Textured = 1 << 4,
    VertexColored = 1 << 5
}

public enum PrimitiveTopology
{
    Points,
    Lines,
    Triangles
}