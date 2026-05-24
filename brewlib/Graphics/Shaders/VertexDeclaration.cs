namespace BrewLib.Graphics.Shaders;

using System;

public sealed class VertexDeclaration
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
        => DrawState.Device.ActivateVertexAttributes(this, shader);

    public void DeactivateAttributes(Shader shader)
        => DrawState.Device.DeactivateVertexAttributes(this, shader);

    public ReadOnlySpan<VertexAttribute>.Enumerator GetEnumerator()
        => ((ReadOnlySpan<VertexAttribute>)vertexAttributes).GetEnumerator();
}