namespace BrewLib.Graphics.Backend.WebGPU;

public sealed class WebGpuRenderPipelineFactory(WebGpuGraphicsBackend backend) : IRenderPipelineFactory
{
    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new WebGpuRenderPipeline(backend, description);
}