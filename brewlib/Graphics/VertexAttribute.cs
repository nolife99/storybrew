namespace BrewLib.Graphics;

using System;
using osuTK.Graphics.OpenGL;

public class VertexAttribute
{
    public const string PositionAttributeName = "a_position", NormalAttributeName = "a_normal",
        TextureCoordAttributeName = "a_textureCoord", ColorAttributeName = "a_color",
        BoneWeightAttributeName = "a_boneWeight", ScaleAttributeName = "a_scale", PresenceAttributeName = "a_presence";

    public int ComponentSize = 4, ComponentCount = 1, Offset;

    public string Name;
    public bool Normalized;
    public VertexAttribPointerType Type = VertexAttribPointerType.Float;
    public AttributeUsage Usage = AttributeUsage.Undefined;

    public string ShaderTypeName => ComponentCount == 1 ? "float" : "vec" + ComponentCount;
    public int Size => ComponentCount * ComponentSize;

    public override bool Equals(object obj)
    {
        if (obj == this) return true;
        if (obj is not VertexAttribute otherAttribute || Name != otherAttribute.Name || Type != otherAttribute.Type ||
            ComponentSize != otherAttribute.ComponentSize || ComponentCount != otherAttribute.ComponentCount ||
            Normalized != otherAttribute.Normalized || Offset != otherAttribute.Offset || Usage != otherAttribute.Usage)
            return false;

        return true;
    }

    public override int GetHashCode()
        => HashCode.Combine(Name, Type, ComponentSize, ComponentCount, Offset, Normalized, Usage);

    public static VertexAttribute CreatePosition2d()
        => new() { Name = PositionAttributeName, ComponentCount = 2, Usage = AttributeUsage.Position };

    public static VertexAttribute CreatePosition3d()
        => new() { Name = PositionAttributeName, ComponentCount = 3, Usage = AttributeUsage.Position };

    public static VertexAttribute CreateNormal()
        => new() { Name = NormalAttributeName, ComponentCount = 3, Usage = AttributeUsage.Normal };

    public static VertexAttribute CreateDiffuseCoord(int index = 0)
        => new()
        {
            Name = TextureCoordAttributeName + index, ComponentCount = 2, Usage = AttributeUsage.DiffuseMapCoord
        };

    public static VertexAttribute CreateColor(bool packed)
        => packed ? new()
        {
            Name = ColorAttributeName,
            ComponentCount = 4,
            ComponentSize = 1,
            Type = VertexAttribPointerType.UnsignedByte,
            Normalized = true,
            Usage = AttributeUsage.Color
        } : new() { Name = ColorAttributeName, ComponentCount = 4, Usage = AttributeUsage.Color };

    public static VertexAttribute CreateBoneWeight(int index = 0)
        => new() { Name = BoneWeightAttributeName + index, ComponentCount = 2, Usage = AttributeUsage.BoneWeight };

    public static VertexAttribute CreateScale()
        => new() { Name = ScaleAttributeName, ComponentCount = 1, Usage = AttributeUsage.Scale };

    public static VertexAttribute CreatePresence()
        => new() { Name = PresenceAttributeName, ComponentCount = 1, Usage = AttributeUsage.Presence };

    public static VertexAttribute CreateVec4(string name, bool packed, AttributeUsage usage)
        => packed ? new()
        {
            Name = name,
            ComponentCount = 4,
            ComponentSize = 1,
            Type = VertexAttribPointerType.UnsignedByte,
            Normalized = true,
            Usage = usage
        } : new() { Name = name, ComponentCount = 4, Usage = usage };

    public static VertexAttribute CreateFloat(string name, AttributeUsage usage)
        => new() { Name = name, ComponentCount = 1, Usage = usage };
}

public enum AttributeUsage
{
    Undefined, Position, Color,
    Normal, DiffuseMapCoord, NormalMapCoord,
    BoneWeight, Scale, Presence
}