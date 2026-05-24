namespace BrewLib.Graphics.Shaders;

using System;

public sealed class VertexAttribute
{
    public const string PositionAttributeName = "a_position", TextureCoordAttributeName = "a_textureCoord",
        ColorAttributeName = "a_color";

    public VertexAttributeFormat Format = VertexAttributeFormat.Float32;
    public string Name;

    public int Offset;
    public AttributeUsage Usage = AttributeUsage.Undefined;

    public int ComponentSize => Format.GetComponentSize();
    public int ComponentCount => Format.GetComponentCount();
    public bool Normalized => Format.IsNormalized();
    public string ShaderTypeName => Format.GetShaderTypeName();
    public int Size => ComponentCount * ComponentSize;

    public override bool Equals(object obj)
        => obj == this ?
            true :
            obj is VertexAttribute otherAttribute && Name == otherAttribute.Name && Format == otherAttribute.Format &&
            Offset == otherAttribute.Offset && Usage == otherAttribute.Usage;

    public override int GetHashCode()
        => HashCode.Combine(Name, Format, Offset, Usage);

    public static VertexAttribute CreatePosition2d(bool packed)
        => packed ?
            new()
            {
                Name = PositionAttributeName,
                Format = VertexAttributeFormat.Float16x2,
                Usage = AttributeUsage.Position
            } :
            new()
            {
                Name = PositionAttributeName,
                Format = VertexAttributeFormat.Float32x2,
                Usage = AttributeUsage.Position
            };

    public static VertexAttribute CreatePosition3d()
        => new()
        {
            Name = PositionAttributeName,
            Format = VertexAttributeFormat.Float32x3,
            Usage = AttributeUsage.Position
        };

    public static VertexAttribute CreateDiffuseCoord(bool packed, int index = 0)
        => packed ?
            new()
            {
                Name = TextureCoordAttributeName + index,
                Format = VertexAttributeFormat.Float16x2,
                Usage = AttributeUsage.DiffuseMapCoord
            } :
            new()
            {
                Name = TextureCoordAttributeName + index,
                Format = VertexAttributeFormat.Float32x2,
                Usage = AttributeUsage.DiffuseMapCoord
            };

    public static VertexAttribute CreateColor(bool packed)
        => packed ?
            new()
            {
                Name = ColorAttributeName,
                Format = VertexAttributeFormat.Unorm8x4,
                Usage = AttributeUsage.Color
            } :
            new()
            {
                Name = ColorAttributeName,
                Format = VertexAttributeFormat.Float32x4,
                Usage = AttributeUsage.Color
            };
}

public enum AttributeUsage : byte
{
    Undefined, Position, Color, DiffuseMapCoord
}