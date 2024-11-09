namespace BrewLib.Graphics;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using osuTK.Graphics.OpenGL;

public class VertexDeclaration : IEnumerable<VertexAttribute>
{
    readonly VertexAttribute[] vertexAttributes;
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

    public VertexAttribute this[int index] => vertexAttributes[index];
    public int AttributeCount => vertexAttributes.Length;

    public VertexAttribute GetAttribute(AttributeUsage usage) => vertexAttributes.FirstOrDefault(a => a.Usage == usage);

    public void ActivateAttributes(Shader shader)
    {
        foreach (var attribute in vertexAttributes)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation < 0) continue;
            GL.EnableVertexAttribArray(attributeLocation);
            GL.VertexAttribPointer(attributeLocation, attribute.ComponentCount, attribute.Type, attribute.Normalized, VertexSize,
                attribute.Offset);
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

    #region Enumerable

    public IEnumerator<VertexAttribute> GetEnumerator() => ((IEnumerable<VertexAttribute>)vertexAttributes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}