namespace BrewLib.Graphics;

using System;
using OpenTK.Graphics.OpenGL;

public class VertexAttribute
{
    public const string PositionAttributeName = "a_position", TextureCoordAttributeName = "a_textureCoord",
        ColorAttributeName = "a_color";

    public int ComponentSize = 4, ComponentCount = 1, Offset;

    public string Name;
    public bool Normalized;
    public VertexAttribType Type = VertexAttribType.Float;
    public AttributeUsage Usage = AttributeUsage.Undefined;

    public string ShaderTypeName => ComponentCount == 1 ? "float" : $"vec{ComponentCount}";
    public int Size => ComponentCount * ComponentSize;

    public override bool Equals(object obj)
    {
        if (obj == this) return true;
        return obj is VertexAttribute otherAttribute && Name == otherAttribute.Name && Type == otherAttribute.Type &&
            ComponentSize == otherAttribute.ComponentSize && ComponentCount == otherAttribute.ComponentCount &&
            Normalized == otherAttribute.Normalized && Offset == otherAttribute.Offset && Usage == otherAttribute.Usage;
    }

    public override int GetHashCode() => HashCode.Combine(Name, Type, ComponentSize, ComponentCount, Offset, Normalized, Usage);

    public static VertexAttribute CreatePosition2d()
        => new() { Name = PositionAttributeName, ComponentCount = 2, Usage = AttributeUsage.Position };

    public static VertexAttribute CreatePosition3d()
        => new() { Name = PositionAttributeName, ComponentCount = 3, Usage = AttributeUsage.Position };

    public static VertexAttribute CreateDiffuseCoord(int index = 0) => new()
    {
        Name = TextureCoordAttributeName + index, ComponentCount = 2, Usage = AttributeUsage.DiffuseMapCoord
    };

    public static VertexAttribute CreateColor(bool packed) => packed ?
        new()
        {
            Name = ColorAttributeName,
            ComponentCount = 4,
            ComponentSize = 1,
            Type = VertexAttribType.UnsignedByte,
            Normalized = true,
            Usage = AttributeUsage.Color
        } :
        new() { Name = ColorAttributeName, ComponentCount = 4, Usage = AttributeUsage.Color };
}

public enum AttributeUsage
{
    Undefined, Position, Color,
    DiffuseMapCoord
}