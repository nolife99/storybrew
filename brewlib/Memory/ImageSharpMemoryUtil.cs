namespace BrewLib.Memory;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Util;

internal sealed class UnmanagedMemoryAllocator : MemoryAllocator
{
    protected override int GetBufferCapacityInBytes() => int.MaxValue;

    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None) =>
        // if (length * Unsafe.SizeOf<T>() < 85000) return new PooledManagedBuffer<T>(length, shared.Value, options);
        new SafeUnmanagedBuffer<T>(length, options);
}

public sealed class SafeUnmanagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly nint addr;
    readonly int length;

    public SafeUnmanagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;

        var byteCount = length * Unsafe.SizeOf<T>();
        addr = options is AllocationOptions.Clean ? Native.ZeroAllocateMemory(byteCount) : Native.AllocateMemory(byteCount);
    }

    [SuppressMessage("Reliability", "CA2015")]
    ~SafeUnmanagedBuffer() => Dispose(false);

    protected override void Dispose(bool disposing) => Native.FreeMemory(addr);
    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), addr), length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)addr, elementIndex));
    public override void Unpin() { }
}

public sealed class PooledManagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly T[] buffer;
    readonly int length;

    GCHandle pinHandle;

    internal PooledManagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;
        buffer = ArrayPool<T>.Shared.Rent(length);

        if (options is AllocationOptions.Clean)
            Unsafe.InitBlock(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetArrayDataReference(buffer)),
                0,
                (uint)(length * Unsafe.SizeOf<T>()));
    }

    protected override void Dispose(bool disposing) => ArrayPool<T>.Shared.Return(buffer);
    public override Span<T> GetSpan() => buffer.AsSpan(0, length);
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