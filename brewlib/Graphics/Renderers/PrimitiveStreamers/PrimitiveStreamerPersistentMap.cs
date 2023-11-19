using BrewLib.Util;
using osuTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BrewLib.Graphics.Renderers.PrimitiveStreamers
{
    public class PrimitiveStreamerPersistentMap<TPrimitive>(VertexDeclaration vertexDeclaration, int minRenderableVertexCount, ushort[] indexes = null) : PrimitiveStreamerVao<TPrimitive>(vertexDeclaration, minRenderableVertexCount, indexes), PrimitiveStreamer<TPrimitive> where TPrimitive : struct
    {
        GpuCommandSync commandSync = new();
        nint bufferPointer;
        int bufferOffset, drawOffset, vertexBufferSize;

        protected override void initializeVertexBuffer()
        {
            base.initializeVertexBuffer();
            vertexBufferSize = MinRenderableVertexCount * VertexDeclaration.VertexSize;

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
            GL.BufferStorage(BufferTarget.ArrayBuffer, vertexBufferSize, default,
                BufferStorageFlags.MapWriteBit | BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit);

            bufferPointer = GL.MapBufferRange(BufferTarget.ArrayBuffer, default, vertexBufferSize,
                BufferAccessMask.MapWriteBit | BufferAccessMask.MapPersistentBit | BufferAccessMask.MapCoherentBit);

            DrawState.CheckError("mapping vertex buffer", bufferPointer == default);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }
        protected override void internalDispose()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
            GL.UnmapBuffer(BufferTarget.ArrayBuffer);

            commandSync.Dispose();
            commandSync = null;

            base.internalDispose();
        }
        public override void Render(PrimitiveType primitiveType, TPrimitive[] primitives, int primitiveCount, int drawCount, bool canBuffer = false)
        {
            if (!Bound) throw new InvalidOperationException("Not bound");

            Debug.Assert(primitiveCount <= primitives.Length);
            Debug.Assert((drawCount & primitiveCount) == 0);

            var vertexDataSize = primitiveCount * PrimitiveSize;
            Debug.Assert(vertexDataSize <= vertexBufferSize);

            if (bufferOffset + vertexDataSize > vertexBufferSize)
            {
                bufferOffset = 0;
                drawOffset = 0;
            }
            if (commandSync.WaitForRange(bufferOffset, vertexDataSize))
            {
                ++BufferWaitCount;
                expandVertexBuffer();
            }

            var pinnedVertexData = GCHandle.Alloc(primitives, GCHandleType.Pinned);
            Native.CopyMemory(primitives.AddrOfPinnedArray(), bufferPointer + bufferOffset, vertexDataSize);
            pinnedVertexData.Free();

            if (IndexBufferId != -1) GL.DrawElements(primitiveType, drawCount, DrawElementsType.UnsignedShort, drawOffset * sizeof(ushort));
            else GL.DrawArrays(primitiveType, drawOffset, drawCount);

            commandSync.LockRange(bufferOffset, vertexDataSize);

            bufferOffset += vertexDataSize;
            drawOffset += drawCount;
        }
        void expandVertexBuffer()
        {
            if (IndexBufferId != -1) return;

            // Prevent the vertex buffer from becoming too large (maxes at 8mb * grow factor)
            if (MinRenderableVertexCount * VertexDeclaration.VertexSize > 8388608) return;

            MinRenderableVertexCount = (int)(MinRenderableVertexCount * 1.75);
            if (commandSync.WaitForAll()) ++BufferWaitCount;

            Unbind();

            // Rebuild the VBO

            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferId);
            GL.UnmapBuffer(BufferTarget.ArrayBuffer);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(VertexBufferId);

            initializeVertexBuffer();

            // Rebuild the VAO

            GL.BindVertexArray(0);
            GL.DeleteVertexArray(VertexArrayId);

            var previousShader = CurrentShader;
            CurrentShader = null;
            Bind(previousShader);

            bufferOffset = 0;
            drawOffset = 0;

            ++DiscardedBufferCount;
        }
        public new static bool HasCapabilities() => DrawState.HasCapabilities(4, 4, "GL_ARB_buffer_storage") &&
            DrawState.HasCapabilities(3, 0, "GL_ARB_map_buffer_range") && DrawState.HasCapabilities(1, 5) && GpuCommandSync.HasCapabilities() &&
            PrimitiveStreamerVao<TPrimitive>.HasCapabilities();
    }
}