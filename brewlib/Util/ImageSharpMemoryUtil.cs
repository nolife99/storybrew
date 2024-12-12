namespace BrewLib.Util;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

internal sealed class UnmanagedMemoryAllocator : MemoryAllocator
{
    protected override int GetBufferCapacityInBytes() => int.MaxValue;
    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        => new UnmanagedBuffer<T>(length, options);
}

public sealed class UnmanagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly int length;

    public UnmanagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;

        var byteCount = length * Marshal.SizeOf<T>();
        Address = Marshal.AllocHGlobal(byteCount);

        if (options is AllocationOptions.Clean)
            Unsafe.InitBlock(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), Address), 0, (uint)byteCount);
    }

    public nint Address { get; }

    protected override void Dispose(bool disposing) => Marshal.FreeHGlobal(Address);
    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), Address), length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)Address, elementIndex));
    public override void Unpin() { }
}

public sealed class PooledManagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly int length;
    GCHandle pinHandle;

    public PooledManagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;
        Buffer = ArrayPool<T>.Shared.Rent(length);

        if (options is AllocationOptions.Clean) Buffer.AsSpan(0, length).Clear();
    }

    public T[] Buffer { get; }

    protected override void Dispose(bool disposing) => ArrayPool<T>.Shared.Return(Buffer);
    public override Span<T> GetSpan() => new(Buffer, 0, length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        if (!pinHandle.IsAllocated) pinHandle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
        return new(Unsafe.Add<T>((void*)pinHandle.AddrOfPinnedObject(), elementIndex), pinHandle, this);
    }
    public override void Unpin()
    {
        if (pinHandle.IsAllocated) pinHandle.Free();
    }
}