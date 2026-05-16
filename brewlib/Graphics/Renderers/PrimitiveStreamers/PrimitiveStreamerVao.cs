namespace BrewLib.Graphics.Renderers.PrimitiveStreamers;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Shaders;
using osuTK.Graphics.OpenGL;

abstract class PrimitiveStreamerVao<TPrimitive> : IPrimitiveStreamer<TPrimitive> where TPrimitive : unmanaged
{
    protected static readonly int PrimitiveSize = Unsafe.SizeOf<TPrimitive>();

    protected readonly int MaxPrimitivesPerBatch;
    protected int indexBufferId = -1, vertexBufferId = -1;

    readonly VertexDeclaration vertexDeclaration;
    Shader currentShader;

    int vertexArrayId = -1;
    bool bound;

    protected PrimitiveStreamerVao(VertexDeclaration vertexDeclaration,
        int maxPrimitivesPerBatch,
        scoped ReadOnlySpan<ushort> indices)
    {
        if (vertexDeclaration.AttributeCount < 1)
            throw new ArgumentException("At least one vertex attribute is required");

        MaxPrimitivesPerBatch = maxPrimitivesPerBatch;
        this.vertexDeclaration = vertexDeclaration;

        initializeVertexBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        if (indices.IsEmpty) return;

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId = GL.GenBuffer());
        GL.BufferData(BufferTarget.ElementArrayBuffer,
            MemoryMarshal.AsBytes(indices).Length,
            ref MemoryMarshal.GetReference(indices),
            BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    public abstract ref TPrimitive PrimitiveAt(int index);

    public void Bind(Shader shader)
    {
        if (bound || shader is null) return;

        if (currentShader != shader) setupVertexArray(shader);

        GL.BindVertexArray(vertexArrayId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId);

        bound = true;
    }

    public void Unbind()
    {
        if (!bound) return;

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        bound = false;
    }

    public abstract void Render(PrimitiveTopology topology, int primitiveCount, int verticesPerPrimitive);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void initializeVertexBuffer()
        => GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId = GL.GenBuffer());

    void setupVertexArray(Shader shader)
    {
        var initial = currentShader is null;
        if (initial) vertexArrayId = GL.GenVertexArray();

        GL.BindVertexArray(vertexArrayId);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBufferId);

        if (!initial) vertexDeclaration.DeactivateAttributes(currentShader);
        vertexDeclaration.ActivateAttributes(shader);

        if (initial && indexBufferId != -1) GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBufferId);

        currentShader = shader;
    }

    ~PrimitiveStreamerVao() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        Unbind();

        if (vertexArrayId != -1) GL.DeleteVertexArray(vertexArrayId);
        if (vertexBufferId != -1) GL.DeleteBuffer(vertexBufferId);
        if (indexBufferId != -1) GL.DeleteBuffer(indexBufferId);
    }

    protected static bool HasCapabilities()
        => DrawState.Backend?.Capabilities.Has(GraphicsBackendFeatures.VertexArrays) ?? false;
}
