namespace BrewLib.Graphics.Shaders;

using System.Collections;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;

public sealed class VertexDeclaration : IEnumerable<VertexAttribute>
{
    readonly VertexAttribute[] vertexAttributes;
    public readonly int VertexSize;

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

    public int AttributeCount => vertexAttributes.Length;

    public VertexAttribute GetAttribute(AttributeUsage usage)
    {
        foreach (var attribute in vertexAttributes)
            if (attribute.Usage == usage)
                return attribute;

        return null;
    }

    public void ActivateAttributes(Shader shader)
    {
        foreach (var attribute in vertexAttributes)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation < 0) continue;

            GL.EnableVertexAttribArray(attributeLocation);
            GL.VertexAttribPointer(attributeLocation,
                attribute.ComponentCount,
                attribute.Type,
                attribute.Normalized,
                VertexSize,
                attribute.Offset);
        }
    }

    public void DeactivateAttributes(Shader shader)
    {
        foreach (var attrib in vertexAttributes)
        {
            var attributeLocation = shader.GetAttributeLocation(attrib.Name);
            if (attributeLocation >= 0) GL.DisableVertexAttribArray(attributeLocation);
        }
    }

    #region Enumerable

    public IEnumerator<VertexAttribute> GetEnumerator()
        => ((IEnumerable<VertexAttribute>)vertexAttributes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}