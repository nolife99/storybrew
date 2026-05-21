namespace BrewLib.Graphics.Backend;

using System;
using System.Collections.Generic;

public readonly record struct UploadRingAllocation(int Offset, int PrimarySize, int SecondarySize)
{
    public int TotalSize => PrimarySize + SecondarySize;
    public bool IsSplit => SecondarySize > 0;
}

public sealed class UploadRingAllocator(int capacityInBytes) : IDisposable
{
    readonly List<ProtectedRange> protectedRanges = new(32);
    bool disposed;
    int nextOffset;

    public int CapacityInBytes { get; } = capacityInBytes;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var range in protectedRanges)
            range.Fence.Dispose();

        protectedRanges.Clear();
        disposed = true;
    }

    public bool TryAllocate(int sizeInBytes, bool allowSplit, out UploadRingAllocation allocation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        allocation = default;
        if (sizeInBytes > CapacityInBytes) return false;

        compactCompletedRanges();

        var offset = nextOffset;
        var tailRoom = CapacityInBytes - offset;

        if (tailRoom >= sizeInBytes)
        {
            if (!waitForOverlaps(offset, sizeInBytes)) return false;

            nextOffset = offset + sizeInBytes;
            if (nextOffset == CapacityInBytes) nextOffset = 0;
            allocation = new(offset, sizeInBytes, 0);
            return true;
        }

        if (allowSplit && tailRoom > 0)
        {
            var headSize = sizeInBytes - tailRoom;
            if (!waitForOverlaps(offset, tailRoom)) return false;
            if (!waitForOverlaps(0, headSize)) return false;

            nextOffset = headSize;
            allocation = new(offset, tailRoom, headSize);
            return true;
        }

        if (!waitForOverlaps(0, sizeInBytes)) return false;

        nextOffset = sizeInBytes;
        allocation = new(0, sizeInBytes, 0);
        return true;
    }

    public UploadRingAllocation Allocate(int sizeInBytes, bool allowSplit)
    {
        if (TryAllocate(sizeInBytes, allowSplit, out var allocation))
            return allocation;

        throw new InvalidOperationException(
            $"Unable to allocate {sizeInBytes} bytes from a {CapacityInBytes} byte upload ring");
    }

    public void Protect(int offset, int sizeInBytes, IGpuUploadFence fence)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes <= 0)
        {
            fence?.Dispose();
            return;
        }

        if (offset < 0 || offset + sizeInBytes > CapacityInBytes)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

        if (fence is null || fence.IsSignaled)
        {
            fence?.Dispose();
            return;
        }

        protectedRanges.Add(new(offset, sizeInBytes, fence));
    }

    bool waitForOverlaps(int offset, int sizeInBytes)
    {
        for (var i = protectedRanges.Count - 1; i >= 0; --i)
        {
            var range = protectedRanges[i];
            if (!overlaps(offset, sizeInBytes, range.Offset, range.SizeInBytes)) continue;

            if (range.Fence.IsSignaled)
            {
                removeRange(i);
                continue;
            }

            if (!range.Fence.CanWait) return false;

            range.Fence.Wait();
            removeRange(i);
        }

        return true;
    }

    void compactCompletedRanges()
    {
        for (var i = protectedRanges.Count - 1; i >= 0; --i)
        {
            if (!protectedRanges[i].Fence.IsSignaled) continue;

            removeRange(i);
        }
    }

    void removeRange(int index)
    {
        protectedRanges[index].Fence.Dispose();
        protectedRanges.RemoveAt(index);
    }

    static bool overlaps(int aOffset, int aSize, int bOffset, int bSize)
        => aOffset < bOffset + bSize && bOffset < aOffset + aSize;

    readonly record struct ProtectedRange(int Offset, int SizeInBytes, IGpuUploadFence Fence);
}
