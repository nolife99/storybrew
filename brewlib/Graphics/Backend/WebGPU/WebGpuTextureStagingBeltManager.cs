namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Ahjo.Wgpu.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

sealed class WebGpuTextureStagingBeltManager : IDisposable
{
    const ulong DefaultChunkSize = 4 * 1024 * 1024;

    readonly WebGpuGraphicsBackend backend;
    readonly StagingBelt belt;
    readonly Lock sync = new();
    bool disposed;

    public WebGpuTextureStagingBeltManager(WebGpuGraphicsBackend backend, ulong chunkSize = DefaultChunkSize)
    {
        this.backend = backend;
        belt = new(backend.DeviceHandle, chunkSize);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        lock (sync)
            belt.Dispose();
    }

    public void Poll()
    {
        if (disposed || backend.IsDeviceLost) return;

        lock (sync)
            belt.Poll();
    }

    public void WriteTexture(Texture texture,
        WGPUTextureFormat format,
        ReadOnlySpan<byte> packedData,
        int width,
        int height,
        int x,
        int y,
        WGPUTextureAspect aspect = WGPUTextureAspect.All)
    {
        if (disposed) throw new ObjectDisposedException(nameof(WebGpuTextureStagingBeltManager));

        backend.ThrowIfDeviceLost();

        CommandBuffer commandBuffer;
        lock (sync)
        {
            belt.Poll();

            var encoder = backend.DeviceHandle.CreateCommandEncoder();
            var encoderCreated = true;
            try
            {
                var extent = new WGPUExtent3D
                {
                    width = (uint)width,
                    height = (uint)height,
                    depthOrArrayLayers = 1
                };

                var origin = new WGPUOrigin3D
                {
                    x = (uint)x,
                    y = (uint)y,
                    z = 0
                };

                belt.WriteTexture(encoder, texture, in extent, format, packedData, 0, origin, aspect);
                belt.Finish();
                commandBuffer = encoder.Finish();
                encoderCreated = false;
            }
            finally
            {
                if (encoderCreated && !backend.IsDeviceLost)
                    encoder.Dispose();
            }
        }

        submitAndRecall(commandBuffer);
    }

    public void WriteTextureRows(Texture texture,
        WGPUTextureFormat format,
        Image<Rgba32> bitmap,
        int width,
        int height,
        int x,
        int y,
        WGPUTextureAspect aspect = WGPUTextureAspect.All)
    {
        if (disposed) throw new ObjectDisposedException(nameof(WebGpuTextureStagingBeltManager));

        backend.ThrowIfDeviceLost();
        if (width <= 0 || height <= 0) return;

        CommandBuffer commandBuffer;
        lock (sync)
        {
            belt.Poll();

            var encoder = backend.DeviceHandle.CreateCommandEncoder();
            var encoderCreated = true;
            try
            {
                var source = bitmap.Frames.RootFrame.PixelBuffer;
                var extent = new WGPUExtent3D
                {
                    width = (uint)width,
                    height = 1,
                    depthOrArrayLayers = 1
                };

                for (var row = 0; row < height; ++row)
                {
                    var origin = new WGPUOrigin3D
                    {
                        x = (uint)x,
                        y = (uint)(y + row),
                        z = 0
                    };

                    var sourceRow = MemoryMarshal.AsBytes(source.DangerousGetRowSpan(row)[..width]);
                    belt.WriteTexture(encoder, texture, in extent, format, sourceRow, 0, origin, aspect);
                }

                belt.Finish();
                commandBuffer = encoder.Finish();
                encoderCreated = false;
            }
            finally
            {
                if (encoderCreated && !backend.IsDeviceLost)
                    encoder.Dispose();
            }
        }

        submitAndRecall(commandBuffer);
    }

    void submitAndRecall(CommandBuffer commandBuffer)
    {
        backend.SubmitCommandBuffer(commandBuffer);

        if (!backend.IsDeviceLost)
            lock (sync)
                belt.Recall();
    }
}