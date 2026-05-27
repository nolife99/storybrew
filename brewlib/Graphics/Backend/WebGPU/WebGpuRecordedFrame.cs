namespace BrewLib.Graphics.Backend.WebGPU;

using System.Numerics;

readonly struct WebGpuRecordedFrame(
    WebGpuFrameCommandList commands,
    WebGpuFrameFlushes flushes,
    Vector4 clearColor,
    bool needsRenderPass,
    bool presentSurfaceTexture)
{
    public readonly WebGpuFrameCommandList Commands = commands;
    public readonly WebGpuFrameFlushes Flushes = flushes;
    public readonly Vector4 ClearColor = clearColor;
    public readonly bool NeedsRenderPass = needsRenderPass;
    public readonly bool PresentSurfaceTexture = presentSurfaceTexture;

    public void Release()
    {
        Commands?.Release();
        Flushes?.Release();
    }
}