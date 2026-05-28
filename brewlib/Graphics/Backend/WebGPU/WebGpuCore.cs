namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BrewLib.Graphics.Textures;
using Ahjo.Wgpu;
using SDL3;
using WgpuBuffer = Ahjo.Wgpu.Buffer;
using WgpuPipelineLayout = Ahjo.Wgpu.PipelineLayout;

public sealed class WebGpuException : InvalidOperationException
{
    public WebGpuException(string message, Exception innerException = null)
        : base(message, innerException) { }

    public static WebGpuException Fatal(string operation, Exception exception)
        => exception is WebGpuException webGpuException
            ? webGpuException
            : new($"WebGPU fatal error while {operation}: {exception.Message}", exception);
}

static class WebGpuInitializer
{
    public static Instance CreateInstance()
    {
        EnableRustDiagnostics();
        var descriptor = new InstanceDescriptor
        {
            Backends = chooseInstanceBackends(),
#if DEBUG
            Flags = InstanceFlags.DevDefault
#else
            Flags = InstanceFlags.None
#endif
        };

        return Instance.Create(in descriptor);
    }

    static void EnableRustDiagnostics()
    {
        setDefaultEnvironmentVariable("RUST_BACKTRACE", "full");
        setDefaultEnvironmentVariable("RUST_LIB_BACKTRACE", "full");
#if DEBUG
        setDefaultEnvironmentVariable("RUST_LOG", "wgpu_core=warn,wgpu_hal=warn,naga=warn");
#endif
    }

    static void setDefaultEnvironmentVariable(string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))) return;
        Environment.SetEnvironmentVariable(name, value);
        try { Process.GetCurrentProcess().StartInfo.Environment[name] = value; }
        catch { }
    }

    static InstanceBackends chooseInstanceBackends()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10)) return InstanceBackends.Dx12;
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS()) return InstanceBackends.Metal;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid()) return InstanceBackends.Vulkan;
        return InstanceBackends.GL;
    }
}

sealed class WebGpuDeferredReleases : IDisposable
{
    readonly Lock sync = new();
    readonly Queue<DeferredRelease> pending = new();
    readonly Queue<FencedBatch> fenced = new();
    readonly WebGpuGraphicsBackend backend;
    bool disposed;

    public WebGpuDeferredReleases(WebGpuGraphicsBackend backend)
        => this.backend = backend;

    public void Retire(BindGroup bindGroup) { if (!bindGroup.IsNull) Retire(DeferredRelease.From(bindGroup)); }
    public void Retire(WgpuBuffer buffer) { if (!buffer.IsNull) Retire(DeferredRelease.From(buffer)); }
    public void Retire(RenderPipeline pipeline) { if (!pipeline.IsNull) Retire(DeferredRelease.From(pipeline)); }
    public void Retire(Sampler sampler) { if (!sampler.IsNull) Retire(DeferredRelease.From(sampler)); }
    public void Retire(Texture texture) { if (!texture.IsNull) Retire(DeferredRelease.From(texture)); }
    public void Retire(TextureView textureView) { if (!textureView.IsNull) Retire(DeferredRelease.From(textureView)); }
    public void Retire(ShaderModule shaderModule) { if (!shaderModule.IsNull) Retire(DeferredRelease.From(shaderModule)); }
    public void Retire(BindGroupLayout layout) { if (!layout.IsNull) Retire(DeferredRelease.From(layout)); }
    public void Retire(WgpuPipelineLayout layout) { if (!layout.IsNull) Retire(DeferredRelease.From(layout)); }
    public void Retire(IDisposable resource) { if (resource is not null) Retire(DeferredRelease.From(resource)); }

    public void Retire(DeferredRelease resource)
    {
        if (resource.IsEmpty) return;
        lock (sync)
        {
            if (disposed) return;
            pending.Enqueue(resource);
        }
    }

    public void AfterQueueSubmit()
    {
        List<DeferredRelease> batch = null;
        lock (sync)
        {
            while (pending.Count != 0)
            {
                batch ??= [];
                batch.Add(pending.Dequeue());
            }
        }

        if (batch is not null)
        {
            QueueWorkDoneRequest request = default;
            try
            {
                request = backend.QueueOnSubmittedWorkDone();
                lock (sync)
                    fenced.Enqueue(new(request, batch));
            }
            catch
            {
                request.Dispose();
                DisposeItems(batch);
                throw;
            }
        }

        ReleaseCompleted();
    }

    public void ReleaseCompleted()
    {
        List<FencedBatch> unfinished = null;
        while (true)
        {
            FencedBatch batch;
            lock (sync)
            {
                if (fenced.Count == 0) break;
                batch = fenced.Dequeue();
            }

            if (batch.Request.IsComplete)
                batch.Release();
            else
            {
                unfinished ??= [];
                unfinished.Add(batch);
            }
        }

        if (unfinished is null) return;
        lock (sync)
            foreach (var batch in unfinished)
                fenced.Enqueue(batch);
    }

    public void WaitForAll()
    {
        while (true)
        {
            var hasPending = false;
            lock (sync)
                hasPending = pending.Count != 0;

            if (hasPending && backend.DeviceHandle is not null)
                AfterQueueSubmit();

            ReleaseCompleted();
            lock (sync)
            {
                if (pending.Count == 0 && fenced.Count == 0) return;
            }

            if (backend.DeviceHandle is not null)
                backend.DeviceHandle.ProcessEvents();

            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        while (true)
        {
            DeferredRelease item = default;
            lock (sync)
            {
                if (pending.Count != 0) item = pending.Dequeue();
            }

            if (item.IsEmpty) break;
            item.Dispose();
        }

        while (true)
        {
            FencedBatch batch = null;
            lock (sync)
            {
                if (fenced.Count != 0) batch = fenced.Dequeue();
            }

            if (batch is null) break;
            batch.Release();
        }
    }

    static void DisposeItems(List<DeferredRelease> items)
    {
        foreach (var item in items)
            item.Dispose();
    }

    sealed class FencedBatch(QueueWorkDoneRequest request, List<DeferredRelease> items)
    {
        public QueueWorkDoneRequest Request = request;

        public void Release()
        {
            try
            {
                DisposeItems(items);
                Request.Dispose();
            }
            finally
            {
                items.Clear();
                Request = default;
            }
        }
    }
}

readonly struct DeferredRelease
{
    readonly DeferredReleaseKind kind;
    readonly IDisposable disposable;
    readonly BindGroup bindGroup;
    readonly WgpuBuffer buffer;
    readonly RenderPipeline renderPipeline;
    readonly Sampler sampler;
    readonly Texture texture;
    readonly TextureView textureView;
    readonly ShaderModule shaderModule;
    readonly BindGroupLayout bindGroupLayout;
    readonly WgpuPipelineLayout pipelineLayout;

    DeferredRelease(DeferredReleaseKind kind,
        IDisposable disposable = null,
        BindGroup bindGroup = default,
        WgpuBuffer buffer = default,
        RenderPipeline renderPipeline = default,
        Sampler sampler = default,
        Texture texture = default,
        TextureView textureView = default,
        ShaderModule shaderModule = default,
        BindGroupLayout bindGroupLayout = default,
        WgpuPipelineLayout pipelineLayout = default)
    {
        this.kind = kind;
        this.disposable = disposable;
        this.bindGroup = bindGroup;
        this.buffer = buffer;
        this.renderPipeline = renderPipeline;
        this.sampler = sampler;
        this.texture = texture;
        this.textureView = textureView;
        this.shaderModule = shaderModule;
        this.bindGroupLayout = bindGroupLayout;
        this.pipelineLayout = pipelineLayout;
    }

    public bool IsEmpty => kind == DeferredReleaseKind.None;

    public static DeferredRelease From(IDisposable disposable) => new(DeferredReleaseKind.Disposable, disposable);
    public static DeferredRelease From(BindGroup bindGroup) => new(DeferredReleaseKind.BindGroup, bindGroup: bindGroup);
    public static DeferredRelease From(WgpuBuffer buffer) => new(DeferredReleaseKind.Buffer, buffer: buffer);
    public static DeferredRelease From(RenderPipeline pipeline) => new(DeferredReleaseKind.RenderPipeline, renderPipeline: pipeline);
    public static DeferredRelease From(Sampler sampler) => new(DeferredReleaseKind.Sampler, sampler: sampler);
    public static DeferredRelease From(Texture texture) => new(DeferredReleaseKind.Texture, texture: texture);
    public static DeferredRelease From(TextureView textureView) => new(DeferredReleaseKind.TextureView, textureView: textureView);
    public static DeferredRelease From(ShaderModule shaderModule) => new(DeferredReleaseKind.ShaderModule, shaderModule: shaderModule);
    public static DeferredRelease From(BindGroupLayout layout) => new(DeferredReleaseKind.BindGroupLayout, bindGroupLayout: layout);
    public static DeferredRelease From(WgpuPipelineLayout layout) => new(DeferredReleaseKind.PipelineLayout, pipelineLayout: layout);

    public void Dispose()
    {
        switch (kind)
        {
            case DeferredReleaseKind.None:
                return;
            case DeferredReleaseKind.Disposable:
                disposable?.Dispose();
                return;
            case DeferredReleaseKind.BindGroup:
                if (!bindGroup.IsNull) bindGroup.Dispose();
                return;
            case DeferredReleaseKind.Buffer:
                if (!buffer.IsNull) buffer.Dispose();
                return;
            case DeferredReleaseKind.RenderPipeline:
                if (!renderPipeline.IsNull) renderPipeline.Dispose();
                return;
            case DeferredReleaseKind.Sampler:
                if (!sampler.IsNull) sampler.Dispose();
                return;
            case DeferredReleaseKind.Texture:
                if (!texture.IsNull) texture.Dispose();
                return;
            case DeferredReleaseKind.TextureView:
                if (!textureView.IsNull) textureView.Dispose();
                return;
            case DeferredReleaseKind.ShaderModule:
                if (!shaderModule.IsNull) shaderModule.Dispose();
                return;
            case DeferredReleaseKind.BindGroupLayout:
                if (!bindGroupLayout.IsNull) bindGroupLayout.Dispose();
                return;
            case DeferredReleaseKind.PipelineLayout:
                if (!pipelineLayout.IsNull) pipelineLayout.Dispose();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}

enum DeferredReleaseKind
{
    None,
    Disposable,
    BindGroup,
    Buffer,
    RenderPipeline,
    Sampler,
    Texture,
    TextureView,
    ShaderModule,
    BindGroupLayout,
    PipelineLayout
}

static class WebGpuResourceValidation
{
    public static WebGpuGraphicsBuffer RequireBuffer(WebGpuGraphicsBackend backend, IGraphicsBuffer buffer, string context)
    {
        if (buffer is not WebGpuGraphicsBuffer webGpuBuffer)
            throw new InvalidOperationException($"{context} must be a WebGPU buffer");
        if (!ReferenceEquals(webGpuBuffer.OwnerBackend, backend))
            throw new InvalidOperationException($"{context} belongs to another backend");
        ObjectDisposedException.ThrowIf(webGpuBuffer.IsDisposed, webGpuBuffer);
        if (webGpuBuffer.BufferHandle.IsNull)
            throw new InvalidOperationException($"{context} has no native buffer allocation");
        return webGpuBuffer;
    }

    public static WebGpuTexture RequireTexture(WebGpuGraphicsBackend backend, ITexture texture, string context)
    {
        if (texture is not WebGpuTexture webGpuTexture)
            throw new InvalidOperationException($"{context} must be a WebGPU texture");
        if (!ReferenceEquals(webGpuTexture.OwnerBackend, backend))
            throw new InvalidOperationException($"{context} belongs to another backend");
        ObjectDisposedException.ThrowIf(webGpuTexture.IsDisposed, webGpuTexture);
        if (webGpuTexture.TextureHandle.IsNull || webGpuTexture.TextureViewHandle.IsNull || webGpuTexture.SamplerHandle.IsNull)
            throw new InvalidOperationException($"{context} has no complete native texture allocation");
        return webGpuTexture;
    }

    public static void ValidateBufferRange(WebGpuGraphicsBuffer buffer, int offset, long requiredBytes, string context)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, $"{context} offset is negative");
        if (requiredBytes < 0) throw new ArgumentOutOfRangeException(nameof(requiredBytes), requiredBytes, $"{context} byte count is negative");
        if ((long)offset + requiredBytes > buffer.SizeInBytes)
            throw new InvalidOperationException($"{context} reads [{offset}, {offset + requiredBytes}) from buffer '{buffer.Description.Name}' of size {buffer.SizeInBytes}");
    }

    public static void ValidateTextureRegion(WebGpuTexture texture, int x, int y, int width, int height, string context)
    {
        if (width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width), $"{context} size is negative");
        if (x < 0 || y < 0 || x + width > texture.Width || y + height > texture.Height)
            throw new ArgumentOutOfRangeException(nameof(x), $"{context} region ({x}, {y}, {width}, {height}) is outside texture {texture.Width}x{texture.Height}");
    }

    public static void ValidateTextureUploadData(ReadOnlySpan<byte> data, int width, int height, int bytesPerPixel, int bytesPerRow, string context)
    {
        if (width <= 0 || height <= 0) return;
        var rowBytes = checked(width * bytesPerPixel);
        if (bytesPerRow < rowBytes)
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow), bytesPerRow, $"{context} bytesPerRow is smaller than row bytes {rowBytes}");
        var required = checked((height - 1) * bytesPerRow + rowBytes);
        if (data.Length < required)
            throw new ArgumentException($"{context} has {data.Length} bytes, needs {required}", nameof(data));
    }

    public static int Align(int value, int alignment)
    {
        if (alignment <= 1) return value;
        var remainder = value % alignment;
        return remainder == 0 ? value : checked(value + alignment - remainder);
    }

    public static int AlignDown(int value, int alignment)
        => alignment <= 1 ? value : value - value % alignment;
}
