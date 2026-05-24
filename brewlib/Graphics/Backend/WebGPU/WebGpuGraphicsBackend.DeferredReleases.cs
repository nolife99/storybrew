namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;

public unsafe sealed partial class WebGpuGraphicsBackend
{
    internal bool IsFrameFenceSignaled(uint frameSerial)
        => unchecked(FrameSerial - frameSerial) >= 1;

    internal void RetireBindGroup(BindGroup* bindGroup)
        => deferredReleases.Retire(bindGroup);

    internal void RetireBuffer(WgpuBuffer* buffer)
        => deferredReleases.Retire(buffer);

    internal void RetireRenderPipeline(RenderPipeline* renderPipeline)
        => deferredReleases.Retire(renderPipeline);

    internal void RetireSampler(Sampler* sampler)
        => deferredReleases.Retire(sampler);

    internal void RetireTexture(Texture* texture)
        => deferredReleases.Retire(texture);

    internal void RetireTextureView(TextureView* textureView)
        => deferredReleases.Retire(textureView);

    internal void RetireDisposable(IDisposable disposable)
        => deferredReleases.Retire(disposable);

    void submitPendingDeferredReleases()
    {
        if (Api is null || QueueHandle is null) return;

        deferredReleases.SubmitPendingWithEmptyQueueWork(Api, QueueHandle);
    }

    void notifyDeferredReleasesAfterQueueSubmit()
    {
        if (Api is null || QueueHandle is null) return;

        deferredReleases.NotifyQueueSubmitted(Api, QueueHandle);
    }

    void releaseCompletedDeferredResources()
    {
        if (Api is null) return;

        deferredReleases.ReleaseCompleted(Api);
    }

    void waitForDeferredReleases()
    {
        if (Api is null)
        {
            deferredReleases.ClearWithoutRelease();
            return;
        }

        deferredReleases.ReleaseAllNow(Api);
    }
}