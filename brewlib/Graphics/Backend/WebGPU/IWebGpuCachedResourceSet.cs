namespace BrewLib.Graphics.Backend.WebGPU;

public interface IWebGpuCachedResourceSet
{
    void PurgeCachedBindGroupsReferencing(WebGpuResourceReference resource);
}