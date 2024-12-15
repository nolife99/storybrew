namespace BrewLib.Util;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Memory;
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
    //  => new UnmanagedBuffer<T>(CreateOrGetArena(length * Unsafe.SizeOf<T>()), length, options);

    RefCountedArenaBuffer ReserveArena(int size = 1 << 28) => RefCountedArenaBuffer.Allocate(manager.Value, size);

    public override void ReleaseRetainedResources()
    {
        foreach (var arena in RefCountedArenaBuffer.Pool) arena.RegisterForDispose();
    }
}

public sealed class SafeUnmanagedBuffer<T> : MemoryManager<T> where T : struct
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

internal sealed class UnmanagedBuffer<T> : MemoryManager<T> where T : struct
{
    readonly RefCountedArenaBuffer _arena;
    readonly int _length;
    readonly nint addr;

    public UnmanagedBuffer(RefCountedArenaBuffer arena, int length, AllocationOptions options)
    {
        _arena = arena;
        _length = length;
        addr = arena.AllocateRange(length * Unsafe.SizeOf<T>());

        if (options is AllocationOptions.Clean && arena.WasReset) GetSpan().Clear();
    }

    protected override void Dispose(bool disposing) => _arena.Release();
    ~UnmanagedBuffer() => Dispose(false);

    public override Span<T> GetSpan()
        => MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), addr), _length);
    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new(Unsafe.Add<T>((void*)addr, elementIndex));
    public override void Unpin() { }
}

internal class RefCountedArenaBuffer : VirtualArena
{
    public static readonly List<RefCountedArenaBuffer> Pool = [];

    static int arenasAllocated;
    readonly Lock allocLock = new();

    bool isDisposeRegistered, release;
    int refCount;

    RefCountedArenaBuffer(VirtualArenaManager manager, string name, VirtualMemoryRange range, uint commitPageSizeMultiplier) :
        base(manager, name, range, commitPageSizeMultiplier) { }
    public bool WasReset { get; private set; }

    public static RefCountedArenaBuffer Allocate(VirtualArenaManager manager, int size)
    {
        var index = Pool.FindIndex(x => x.CapacityInBytes == (nuint)size);
        if (index != -1)
        {
            var arena = Pool[index];
            Pool.RemoveAt(index);
            return arena;
        }

        var commitPageSizeMultiplier = manager.DefaultCommitPageSizeMultiplier;
        var capacityInBytes = (nuint)size;

        capacityInBytes = VirtualMemoryHelper.AlignToUpper(capacityInBytes, manager.Handler.PageSize * commitPageSizeMultiplier);

        var range = manager.Handler.TryReserve(capacityInBytes);
        if (range.IsNull) throw new VirtualMemoryException($"Cannot reserve {capacityInBytes} bytes");

        return new(manager, arenasAllocated++.ToString(CultureInfo.CurrentCulture), range, commitPageSizeMultiplier);
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
            else Reset(VirtualArenaResetKind.KeepAllCommitted);

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
        else Reset(VirtualArenaResetKind.KeepAllCommitted);
    }

    protected override void DisposeImpl()
    {
        Trace.WriteLine($"Disposed arena {Name}");
        release = false;
    }

    protected override void ResetImpl()
    {
        Trace.WriteLine($"Reset arena {Name}");
        WasReset = true;

        isDisposeRegistered = false;

        Pool.Add(this);
    }
}