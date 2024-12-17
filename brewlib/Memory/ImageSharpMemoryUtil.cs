namespace BrewLib.Memory;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Memory;
using Util;
using Varena;

internal sealed class UnmanagedMemoryAllocator : MemoryAllocator
{
    readonly Lazy<VirtualArenaManager> manager = new();

    RefCountedArenaBuffer currentArena;
    RefCountedArenaBuffer CreateOrGetArena(int byteSize)
    {
        if (currentArena is null || currentArena.IsDisposed) currentArena = ReserveArena();
        else if (currentArena.AvailableInBytes < (nuint)byteSize)
        {
            currentArena.RegisterForDispose(false);
            currentArena = ReserveArena();
        }

        return currentArena;
    }

    protected override int GetBufferCapacityInBytes() => int.MaxValue;

    public override IMemoryOwner<T> Allocate<T>(int length, AllocationOptions options = AllocationOptions.None)
        => new SafeUnmanagedBuffer<T>(length, options);
    //  => new UnmanagedBuffer<T>(CreateOrGetArena(length * Unsafe.SizeOf<T>()), length);

    RefCountedArenaBuffer ReserveArena() => RefCountedArenaBuffer.Allocate(manager.Value);

    public override void ReleaseRetainedResources()
    {
        foreach (var arena in RefCountedArenaBuffer.Pool) arena.RegisterForDispose();
    }
}

internal sealed class SafeUnmanagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly nint addr;
    readonly int length;

    public SafeUnmanagedBuffer(int length, AllocationOptions options = AllocationOptions.None)
    {
        this.length = length;

        var byteCount = length * Unsafe.SizeOf<T>();
        addr = Native.AllocateMemory(byteCount);

        if (options is AllocationOptions.Clean)
            Unsafe.InitBlock(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), addr), 0, (uint)byteCount);
    }

    ~SafeUnmanagedBuffer() => Dispose(false);
    protected override void Dispose(bool disposing) => Native.FreeMemory(addr);
    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), addr), length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)addr, elementIndex));
    public override void Unpin() { }
}

internal sealed class UnmanagedBuffer<T>(RefCountedArenaBuffer arena, int length) : MemoryManager<T> where T : struct
{
    readonly nint addr = arena.AllocateRange(length * Unsafe.SizeOf<T>());

    protected override void Dispose(bool disposing) => arena.Release();
    ~UnmanagedBuffer() => Dispose(false);

    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), addr), length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)addr, elementIndex));
    public override void Unpin() { }
}

internal class RefCountedArenaBuffer : VirtualArena
{
    const nuint DefaultArenaSize = 1 << 28;
    public static readonly ConcurrentQueue<RefCountedArenaBuffer> Pool = [];

    static int arenasAllocated;
    readonly Lock allocLock = new();

    bool isDisposeRegistered, release;
    int refCount;

    RefCountedArenaBuffer(VirtualArenaManager manager, string name, VirtualMemoryRange range, uint commitPageSizeMultiplier) :
        base(manager, name, range, commitPageSizeMultiplier) { }

    public static RefCountedArenaBuffer Allocate(VirtualArenaManager manager)
    {
        if (Pool.TryDequeue(out var arena)) return arena;

        var commitPageSizeMultiplier = manager.DefaultCommitPageSizeMultiplier;
        var capacityInBytes =
            VirtualMemoryHelper.AlignToUpper(DefaultArenaSize, manager.Handler.PageSize * commitPageSizeMultiplier);

        var range = manager.Handler.TryReserve(capacityInBytes);
        if (range.IsNull) throw new VirtualMemoryException($"Cannot reserve {capacityInBytes} bytes");

        return new(manager, arenasAllocated++.ToString(CultureInfo.InvariantCulture), range, commitPageSizeMultiplier);
    }

    public unsafe nint AllocateRange(int count)
    {
        void* addr;
        using (allocLock.EnterScope()) addr = UnsafeAllocate((nuint)count);
        Interlocked.Increment(ref refCount);
        return (nint)addr;
    }

    public void RegisterForDispose(bool forceDispose = true)
    {
        if (refCount == 0)
        {
            if (forceDispose) Dispose();
            else Reset();

            return;
        }

        Trace.WriteLine(
            $"Registering arena {Name} for decommit: {StringHelper.ToByteSize(AllocatedBytes)} / {StringHelper.ToByteSize(CapacityInBytes)} allocated | {refCount} references");

        isDisposeRegistered = true;
        if (forceDispose) release = true;
    }

    public void Release()
    {
        Interlocked.Decrement(ref refCount);
        if (refCount != 0 || !isDisposeRegistered) return;

        if (release) Dispose();
        else Reset();
    }

    protected override void DisposeImpl()
    {
        Trace.WriteLine($"Disposed arena {Name}");
        release = false;
    }

    protected override void ResetImpl()
    {
        Trace.WriteLine($"Reset arena {Name}");
        isDisposeRegistered = false;

        Pool.Enqueue(this);
    }
}