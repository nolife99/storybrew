namespace BrewLib.Graphics.Shaders;

using System;

public enum VertexAttributeFormat
{
    Float32,
    Float32x2,
    Float32x3,
    Float32x4,
    Float16x2,
    Float16x4,
    Unorm8x4
}

public static class VertexAttributeFormatExtensions
{
    public static int GetComponentCount(this VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 => 1,
            VertexAttributeFormat.Float32x2 or VertexAttributeFormat.Float16x2 => 2,
            VertexAttributeFormat.Float32x3 => 3,
            VertexAttributeFormat.Float32x4 or VertexAttributeFormat.Float16x4 or VertexAttributeFormat.Unorm8x4 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    public static int GetComponentSize(this VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 or
                VertexAttributeFormat.Float32x2 or
                VertexAttributeFormat.Float32x3 or
                VertexAttributeFormat.Float32x4 => 4,
            VertexAttributeFormat.Float16x2 or VertexAttributeFormat.Float16x4 => 2,
            VertexAttributeFormat.Unorm8x4 => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    public static bool IsNormalized(this VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Unorm8x4 => true,
            VertexAttributeFormat.Float32 or
                VertexAttributeFormat.Float32x2 or
                VertexAttributeFormat.Float32x3 or
                VertexAttributeFormat.Float32x4 or
                VertexAttributeFormat.Float16x2 or
                VertexAttributeFormat.Float16x4 => false,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    public static string GetShaderTypeName(this VertexAttributeFormat format)
    {
        var componentCount = format.GetComponentCount();
        return componentCount == 1 ? "float" : $"vec{componentCount}";
    }
}
