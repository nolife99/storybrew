namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Wgpu;
using SDL3;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

sealed class WebGpuBufferFactory : IGraphicsBufferFactory
{
    readonly WebGpuBackend backend;
    readonly WebGpuDeviceContext deviceContext;

    public WebGpuBufferFactory(WebGpuBackend backend, WebGpuDeviceContext deviceContext)
    {
        this.backend = backend;
        this.deviceContext = deviceContext;
    }

    public IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description)
        => new WebGpuGraphicsBuffer(backend, deviceContext, description);
}

sealed class WebGpuGraphicsBuffer : IGraphicsBuffer
{
    readonly WebGpuBackend backend;
    readonly GraphicsBufferDescription description;
    readonly WebGpuDeviceContext deviceContext;
    readonly bool isRing;
    readonly int offsetAlignment;
    bool disposed;
    int frameWriteCursor;

    WgpuBuffer wgpuBuffer;

    public WebGpuGraphicsBuffer(WebGpuBackend backend, WebGpuDeviceContext deviceContext, GraphicsBufferDescription description)
    {
        this.backend = backend;
        this.deviceContext = deviceContext;
        this.description = description;

        isRing = description.Usage is GraphicsBufferUsage.Dynamic or GraphicsBufferUsage.Stream;
        offsetAlignment = description.Target == GraphicsBufferTarget.Uniform
            ? (int)deviceContext.MinUniformOffsetAlignment
            : 4;

        if (description.SizeInBytes > 0)
            EnsureCapacity(WgpuMapper.AlignUp(description.SizeInBytes, 4), false);

        if (isRing) backend.RegisterRingBuffer(this);
    }

    public int CapacityBytes { get; private set; }

    public WgpuBuffer Buffer => wgpuBuffer;

    public int CurrentReadOffset { get; private set; }

    public GraphicsBufferDescription Description => description;
    public int SizeInBytes { get; private set; }

    public void Allocate(int size)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        if (size == 0) return;

        EnsureCapacity(WgpuMapper.AlignUp(size, 4), description.Usage == GraphicsBufferUsage.Static);
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ThrowIfDisposed();
        var byteSpan = MemoryMarshal.AsBytes(data);
        if (byteSpan.Length == 0)
        {
            SizeInBytes = 0;
            CurrentReadOffset = 0;
            return;
        }

        if (description.Usage == GraphicsBufferUsage.Static)
            WriteStatic(byteSpan);
        else
            WriteStreaming(byteSpan);
    }

    public void Invalidate()
    {
        SizeInBytes = 0;
        CurrentReadOffset = 0;
        frameWriteCursor = 0;
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        try
        {
            Disposing?.Invoke(this);
        }
        catch (Exception ex)
        {
            SDL.LogError(LogCategory.Render, $"WebGpuGraphicsBuffer.Disposing handler threw: {ex.Message}");
        }

        Disposing = null;
        Resized = null;

        if (isRing) backend.UnregisterRingBuffer(this);

        if (!wgpuBuffer.IsNull)
        {
            backend.EnqueueDeferredDisposal(new DisposableHandle(wgpuBuffer));
            wgpuBuffer = default;
        }

        CapacityBytes = 0;
        SizeInBytes = 0;
    }

    public event Action<WebGpuGraphicsBuffer> Resized;
    public event Action<WebGpuGraphicsBuffer> Disposing;

    public void ResetFrame()
    {
        frameWriteCursor = 0;
        CurrentReadOffset = 0;
    }

    void WriteStatic(scoped ReadOnlySpan<byte> bytes)
    {
        var paddedLen = WgpuMapper.AlignUp(bytes.Length, 4);
        var bufDesc = new BufferDescriptor
        {
            Size = (ulong)paddedLen,
            Usage = TranslateUsage(description.Target),
            MappedAtCreation = true
        };

        var fresh = deviceContext.Device.CreateBuffer(in bufDesc);
        try
        {
            var mapped = fresh.GetMappedRange<byte>(0, (UIntPtr)paddedLen);
            bytes.CopyTo(mapped);
            if (paddedLen > bytes.Length) mapped[bytes.Length..].Clear();
            fresh.Unmap();
        }
        catch
        {
            fresh.Dispose();
            throw;
        }

        ReplaceWith(fresh, paddedLen);
        SizeInBytes = bytes.Length;
        CurrentReadOffset = 0;
    }

    void WriteStreaming(scoped ReadOnlySpan<byte> bytes)
    {
        var paddedLen = WgpuMapper.AlignUp(bytes.Length, 4);
        var offset = isRing ? WgpuMapper.AlignUp(frameWriteCursor, offsetAlignment) : 0;
        var required = offset + paddedLen;

        if (required > CapacityBytes || wgpuBuffer.IsNull)
        {
            var newCap = CapacityBytes == 0 ? Math.Max(required, 256) : CapacityBytes;
            while (newCap < required) newCap *= 2;
            EnsureCapacity(newCap, false);
        }

        if (paddedLen == bytes.Length)
        {
            deviceContext.Queue.WriteBuffer(wgpuBuffer, (ulong)offset, bytes);
        }
        else
        {
            var tmp = paddedLen <= 1024 ? stackalloc byte[paddedLen] : new byte[paddedLen];
            bytes.CopyTo(tmp);
            tmp[bytes.Length..].Clear();
            deviceContext.Queue.WriteBuffer(wgpuBuffer, (ulong)offset, tmp);
        }

        CurrentReadOffset = offset;
        SizeInBytes = bytes.Length;
        if (isRing) frameWriteCursor = offset + paddedLen;
    }

    void EnsureCapacity(int requiredBytes, bool forceReplace)
    {
        if (!forceReplace && requiredBytes <= CapacityBytes && !wgpuBuffer.IsNull) return;

        var bufDesc = new BufferDescriptor
        {
            Size = (ulong)requiredBytes,
            Usage = TranslateUsage(description.Target),
            MappedAtCreation = false
        };

        var fresh = deviceContext.Device.CreateBuffer(in bufDesc);
        ReplaceWith(fresh, requiredBytes);
    }

    void ReplaceWith(WgpuBuffer fresh, int newCapacity)
    {
        if (!wgpuBuffer.IsNull)
            backend.EnqueueDeferredDisposal(new DisposableHandle(wgpuBuffer));

        wgpuBuffer = fresh;
        CapacityBytes = newCapacity;

        try
        {
            Resized?.Invoke(this);
        }
        catch (Exception ex)
        {
            SDL.LogError(LogCategory.Render, $"WebGpuGraphicsBuffer.Resized handler threw: {ex.Message}");
        }
    }

    void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    static BufferUsage TranslateUsage(GraphicsBufferTarget target)
    {
        var flags = target switch
        {
            GraphicsBufferTarget.Vertex => BufferUsage.Vertex,
            GraphicsBufferTarget.Index => BufferUsage.Index,
            GraphicsBufferTarget.Uniform => BufferUsage.Uniform,
            GraphicsBufferTarget.Storage => BufferUsage.Storage,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported buffer target")
        };

        flags |= BufferUsage.CopyDst;
        if (target == GraphicsBufferTarget.Storage) flags |= BufferUsage.CopySrc;
        return flags;
    }

    sealed class DisposableHandle : IDisposable
    {
        WgpuBuffer buffer;
        public DisposableHandle(WgpuBuffer b) => buffer = b;

        public void Dispose()
        {
            if (buffer.IsNull) return;

            buffer.Dispose();
            buffer = default;
        }
    }
}