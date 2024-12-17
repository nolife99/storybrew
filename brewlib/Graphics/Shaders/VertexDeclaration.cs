namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;

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

    public int AttributeCount => vertexAttributes.Length;

    public VertexAttribute GetAttribute(AttributeUsage usage) => Array.Find(vertexAttributes, a => a.Usage == usage);

    public void ActivateAttributes(Shader shader, int vao)
    {
        foreach (var attribute in vertexAttributes)
        {
            var attributeLocation = shader.GetAttributeLocation(attribute.Name);
            if (attributeLocation < 0) continue;

            GL.EnableVertexArrayAttrib(vao, attributeLocation);
            GL.VertexArrayAttribFormat(vao,
                attributeLocation,
                attribute.ComponentCount,
                attribute.Type,
                attribute.Normalized,
                attribute.Offset);

            GL.VertexArrayAttribBinding(vao, attributeLocation, 0);
        }
    }
    public void DeactivateAttributes(Shader shader, int vao)
    {
        for (var i = 0; i < vertexAttributes.Length; i++)
        {
            var attributeLocation = shader.GetAttributeLocation(vertexAttributes[i].Name);
            if (attributeLocation >= 0) GL.DisableVertexArrayAttrib(vao, attributeLocation);
        }
    }

    #region Enumerable

    public IEnumerator<VertexAttribute> GetEnumerator() => ((IEnumerable<VertexAttribute>)vertexAttributes).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}