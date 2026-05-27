namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;

sealed class WebGpuFrameResources : IDisposable
{
    readonly List<IWebGpuCachedResourceSet> cachedResourceSets = [];
    WebGpuFrameFlushes flushes = WebGpuFrameFlushes.Rent();

    public void Dispose() => flushes.Release();

    public void RegisterPipelineForUniformFlush(WebGpuRenderPipeline pipeline)
        => flushes.Add(pipeline);

    public void RegisterBufferUpload(WebGpuGraphicsBuffer buffer)
        => flushes.Add(buffer);

    public void RegisterVertexUpload(WebGpuVertexUpload upload)
        => flushes.Add(upload);

    public WebGpuFrameFlushes DetachFlushes()
    {
        var detached = flushes;
        detached.DetachUploads();
        flushes = WebGpuFrameFlushes.Rent();
        return detached;
    }

    public void RegisterCachedResourceSet(IWebGpuCachedResourceSet resourceSet)
        => cachedResourceSets.Add(resourceSet);

    public void UnregisterCachedResourceSet(IWebGpuCachedResourceSet resourceSet)
        => cachedResourceSets.Remove(resourceSet);

    public void PurgeCachedBindGroupsReferencing(WebGpuResourceReference resource)
    {
        if (resource.IsNull) return;

        for (var i = cachedResourceSets.Count - 1; i >= 0; --i)
            cachedResourceSets[i].PurgeCachedBindGroupsReferencing(resource);
    }
}