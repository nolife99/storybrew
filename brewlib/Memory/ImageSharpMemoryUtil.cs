namespace BrewLib.Memory;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Memory;
using Util;

internal sealed class UnmanagedMemoryAllocator : MemoryAllocator
{
    readonly Lazy<ArrayPool<byte>> shared = new(ArrayPool<byte>.Create, LazyThreadSafetyMode.PublicationOnly);

    protected override int GetBufferCapacityInBytes() => int.MaxValue;

    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None) =>
        // if (length * Unsafe.SizeOf<T>() < 1 << 20) return new PooledManagedBuffer<T>(length, shared.Value, options);
        new SafeUnmanagedBuffer<T>(length, options);
}

public sealed class SafeUnmanagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly int length;

    public SafeUnmanagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;

        var byteCount = length * Unsafe.SizeOf<T>();
        Address = options is AllocationOptions.Clean ? Native.ZeroAllocateMemory(byteCount) : Native.AllocateMemory(byteCount);
    }
    public nint Address { get; }

    [SuppressMessage("Reliability", "CA2015")]
    ~SafeUnmanagedBuffer() => Dispose(false);

    protected override void Dispose(bool disposing) => Native.FreeMemory(Address);
    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), Address), length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)Address, elementIndex));
    public override void Unpin() { }
}

public sealed class PooledManagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly byte[] buffer;
    readonly int length;
    readonly ArrayPool<byte> pool;

    GCHandle pinHandle;

    internal PooledManagedBuffer(int length, ArrayPool<byte> pool, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;
        this.pool = pool;

        buffer = pool.Rent(length * Unsafe.SizeOf<T>());
        if (options is AllocationOptions.Clean)
            Unsafe.InitBlock(ref MemoryMarshal.GetArrayDataReference(buffer), 0, (uint)(length * Unsafe.SizeOf<T>()));
    }

    protected override void Dispose(bool disposing) => pool.Return(buffer);
    public override Span<T> GetSpan() => MemoryMarshal.Cast<byte, T>(buffer)[..length];
    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if (!pinHandle.IsAllocated) pinHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        return new(Unsafe.Add<T>((void*)pinHandle.AddrOfPinnedObject(), elementIndex), pinHandle, this);
    }
    public override void Unpin()
    {
        if (pinHandle.IsAllocated) pinHandle.Free();
    }
}