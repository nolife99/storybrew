namespace BrewLib.Graphics.Backend.WebGPU;

using System.Collections.Concurrent;
using System.Collections.Generic;
using Ahjo.Wgpu;

sealed class WebGpuFrameFlushes
{
    static readonly ConcurrentBag<WebGpuFrameFlushes> pool = [];
    readonly List<WebGpuGraphicsBuffer> buffers = [];
    readonly List<WebGpuBufferUpload> bufferUploads = [];

    readonly List<WebGpuRenderPipeline> pipelines = [];
    readonly List<WebGpuVertexUpload> vertexUploads = [];

    public static WebGpuFrameFlushes Rent()
        => pool.TryTake(out var flushes) ? flushes : new();

    public void Add(WebGpuRenderPipeline pipeline)
        => pipelines.Add(pipeline);

    public void Add(WebGpuGraphicsBuffer buffer)
        => buffers.Add(buffer);

    public void Add(WebGpuVertexUpload upload)
        => vertexUploads.Add(upload);

    public void DetachUploads()
    {
        for (var i = 0; i < buffers.Count; ++i)
        {
            var upload = buffers[i].DetachPendingUpload();
            if (!upload.IsEmpty)
                bufferUploads.Add(upload);
        }

        buffers.Clear();
    }

    public void Flush(WebGpuGraphicsBackend backend, CommandEncoder encoder)
    {
        if (vertexUploads.Count != 0)
        {
            for (var i = 0; i < vertexUploads.Count; ++i)
                vertexUploads[i].Flush(backend.VertexStagingBelt, encoder);

            vertexUploads.Clear();
        }

        for (var i = 0; i < pipelines.Count; ++i)
            pipelines[i].FlushUniforms();

        for (var i = 0; i < bufferUploads.Count; ++i)
        {
            bufferUploads[i].Flush(backend);
            bufferUploads[i] = default;
        }

        bufferUploads.Clear();
    }

    public void Release()
    {
        pipelines.Clear();
        buffers.Clear();
        for (var i = 0; i < bufferUploads.Count; ++i)
            bufferUploads[i].Release();

        bufferUploads.Clear();
        for (var i = 0; i < vertexUploads.Count; ++i)
            vertexUploads[i].Release();

        vertexUploads.Clear();
        pool.Add(this);
    }
}