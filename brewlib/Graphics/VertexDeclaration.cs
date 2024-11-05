using System.Collections;
using System.Collections.Generic;
using System.Linq;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics;

public class VertexDeclaration : IEnumerable<VertexAttribute>
{
    static int nextId;
    public readonly int Id = nextId++;
    readonly VertexAttribute[] vertexAttributes;

    public VertexAttribute this[int index] => vertexAttributes[index];
    public int AttributeCount => vertexAttributes.Length;
    public int VertexSize;

    public VertexDeclaration(params VertexAttribute[] vertexAttributes)
    {
        this.vertexAttributes = vertexAttributes;

        VertexSize = 0;
        foreach (var attribute in vertexAttributes)
        {
            attribute.Offset = VertexSize;
            VertexSize += attribute.Size;
        }
    }

    public VertexAttribute GetAttribute(AttributeUsage usage) => vertexAttributes.FirstOrDefault(a => a.Usage == usage);
    public void ActivateAttributes(Shader shader)
    {
        foreach (var attribute in vertexAttributes)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation >= 0)
            {
                GL.EnableVertexAttribArray(attributeLocation);
                GL.VertexAttribPointer(attributeLocation, attribute.ComponentCount, attribute.Type, attribute.Normalized, VertexSize, attribute.Offset);
            }
        }
    }
    public void DeactivateAttributes(Shader shader)
    {
        foreach (var attribute in vertexAttributes)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation >= 0) GL.DisableVertexAttribArray(attributeLocation);
        }
    }
    public override bool Equals(object obj)
    {
        if (obj == this) return true;

        if (obj is not VertexDeclaration otherDeclaration) return false;
        if (AttributeCount != otherDeclaration.AttributeCount) return false;
        for (var i = 0; i < AttributeCount; i++) if (!this[i].Equals(otherDeclaration[i])) return false;

        return true;
    }
    public override int GetHashCode() => base.GetHashCode();

    #region Enumerable

    public IEnumerator<VertexAttribute> GetEnumerator() => ((IEnumerable<VertexAttribute>)vertexAttributes).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}