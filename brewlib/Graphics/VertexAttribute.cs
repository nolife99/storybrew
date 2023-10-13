using OpenTK.Graphics.OpenGL;

namespace BrewLib.Graphics
{
    public class VertexAttribute
    {
        public const string PositionAttributeName = "a_position", 
            NormalAttributeName = "a_normal",
            TextureCoordAttributeName = "a_textureCoord",
            ColorAttributeName = "a_color",
            BoneWeightAttributeName = "a_boneWeight",
            ScaleAttributeName = "a_scale",
            PresenceAttributeName = "a_presence";

        public string Name;
        public VertexAttribPointerType Type = VertexAttribPointerType.Float;
        public int ComponentSize = 4, ComponentCount = 1;
        public bool Normalized;
        public int Offset;
        public AttributeUsage Usage = AttributeUsage.Undefined;

        public string ShaderTypeName => ComponentCount == 1 ? "float" : "vec" + ComponentCount;
        public int Size => ComponentCount * ComponentSize;

        public override bool Equals(object other)
        {
            if (other == this) return true;

            var otherAttribute = other as VertexAttribute;
            if (otherAttribute is null || 
                Name != otherAttribute.Name || Type != otherAttribute.Type ||
                ComponentSize != otherAttribute.ComponentSize || ComponentCount != otherAttribute.ComponentCount ||
                Normalized != otherAttribute.Normalized || Offset != otherAttribute.Offset || Usage != otherAttribute.Usage) 
                return false;

            return true;
        }
        public override int GetHashCode() => base.GetHashCode();
        public override string ToString() => $"{Name} {ComponentCount}x {Type} (used as {Usage})";

        public static VertexAttribute CreatePosition2d() => new VertexAttribute
        {
            Name = PositionAttributeName,
            ComponentCount = 2,
            Usage = AttributeUsage.Position
        };
        public static VertexAttribute CreatePosition3d() => new VertexAttribute
        {
            Name = PositionAttributeName,
            ComponentCount = 3,
            Usage = AttributeUsage.Position
        };
        public static VertexAttribute CreateNormal() => new VertexAttribute
        {
            Name = NormalAttributeName,
            ComponentCount = 3,
            Usage = AttributeUsage.Normal
        };
        public static VertexAttribute CreateDiffuseCoord(int index = 0) => new VertexAttribute
        {
            Name = TextureCoordAttributeName + index,
            ComponentCount = 2,
            Usage = AttributeUsage.DiffuseMapCoord
        };
        public static VertexAttribute CreateColor(bool packed) => packed ? new VertexAttribute
        {
            Name = ColorAttributeName,
            ComponentCount = 4,
            ComponentSize = 1,
            Type = VertexAttribPointerType.UnsignedByte,
            Normalized = true,
            Usage = AttributeUsage.Color
        } :
        new VertexAttribute
        {
            Name = ColorAttributeName,
            ComponentCount = 4,
            Usage = AttributeUsage.Color
        };
        public static VertexAttribute CreateBoneWeight(int index = 0) => new VertexAttribute
        {
            Name = BoneWeightAttributeName + index,
            ComponentCount = 2,
            Usage = AttributeUsage.BoneWeight
        };
        public static VertexAttribute CreateScale() => new VertexAttribute
        {
            Name = ScaleAttributeName,
            ComponentCount = 1,
            Usage = AttributeUsage.Scale
        };
        public static VertexAttribute CreatePresence() => new VertexAttribute
        {
            Name = PresenceAttributeName,
            ComponentCount = 1,
            Usage = AttributeUsage.Presence
        };
        public static VertexAttribute CreateVec4(string name, bool packed, AttributeUsage usage) => packed ? new VertexAttribute
        {
            Name = name,
            ComponentCount = 4,
            ComponentSize = 1,
            Type = VertexAttribPointerType.UnsignedByte,
            Normalized = true,
            Usage = usage
        } :
        new VertexAttribute
        {
            Name = name,
            ComponentCount = 4,
            Usage = usage
        };
        public static VertexAttribute CreateFloat(string name, AttributeUsage usage) => new VertexAttribute
        {
            Name = name,
            ComponentCount = 1,
            Usage = usage
        };
    }
    public enum AttributeUsage
    {
        Undefined, Position,
        Color, Normal,
        DiffuseMapCoord, NormalMapCoord,
        BoneWeight,
        Scale, Presence
    }
}