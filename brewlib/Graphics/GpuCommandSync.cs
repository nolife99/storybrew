namespace BrewLib.Graphics;

using System;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public sealed class GpuCommandSync : IDisposable
{
    // Active fences with their guarded sorted ranges
    ValueQueue<FenceRecord> active = ValueQueue<FenceRecord>.Create();

    bool disposed;

    // Accumulate subranges written in the upcoming draw
    ValueDictionary<int, ValueList<Range>> pending = ValueDictionary.Create<int, ValueList<Range>>();

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        WaitForAll();

        active.Dispose();
        foreach (var list in pending.Values) list.Dispose();

        pending.Dispose();
    }

    // Utility method for less boilerplate
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WaitAndLockRange(int bufferId, int offset, int size)
    {
        WaitForRange(bufferId, offset, size);
        LockRange(bufferId, offset, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WaitAndLockRangeWrap(int bufferId, int offset, int size, int bufferSize)
    {
        WaitForRangeWrap(bufferId, offset, size, bufferSize);
        LockRangeWrap(bufferId, offset, size, bufferSize);
    }

    // Guard a written range for a buffer by the next CommitPending call: <[offset, offset+size) in bytes>
    public void LockRange(int bufferId, int offset, int size)
    {
        if (size <= 0) return;

        ref var list = ref GetOrCreateRanges(ref pending, bufferId);
        AddMerged(ref list, new() { Start = offset, End = offset + size });
    }

    // Extension for ring buffers where a write may wrap around
    public void LockRangeWrap(int bufferId, int offset, int size, int bufferSize)
    {
        if (size <= 0) return;

        var end = offset + size;
        if (end <= bufferSize) LockRange(bufferId, offset, size);
        else
        {
            var first = bufferSize - offset;
            LockRange(bufferId, offset, first);
            LockRange(bufferId, 0, size - first);
        }
    }

    // Waits until the specified range is safe to write (no overlap with guarded ranges)
    public void WaitForRange(int bufferId, int offset, int size)
    {
        if (size <= 0) return;

        Range target = new() { Start = offset, End = offset + size };

        // Walk active fences in order, only wait on those that actually protect overlapping ranges for this buffer
        var count = active.Count;
        for (var i = 0; i < count; i++)
        {
            var record = active.Peek();

            if (IsFenceSignaled(record.Fence))
            {
                PopAndDeleteFrontFence();
                continue;
            }

            if (record.RangesByBuffer.TryGetValue(bufferId, out var ranges) && OverlapsAny(in ranges, target))
            {
                GL.ClientWaitSync(record.Fence, ClientWaitSyncFlags.SyncFlushCommandsBit, ulong.MaxValue);
                PopAndDeleteFrontFence();

                // Continue checking in case later fences also overlap (extremely unlikely)
            }

            // This fence doesn't guard this range, rotate it to the back and continue
            else active.Enqueue(active.Dequeue());
        }
    }

    // For ring buffers where a wait range may wrap around
    public void WaitForRangeWrap(int bufferId, int offset, int size, int bufferSize)
    {
        if (size <= 0) return;

        var end = offset + size;
        if (end <= bufferSize) WaitForRange(bufferId, offset, size);
        else
        {
            var first = bufferSize - offset;
            WaitForRange(bufferId, offset, first);
            WaitForRange(bufferId, 0, size - first);
        }
    }

    // Inserts a single fence guarding all ranges added after the last commit
    // Should only be called directly after a GL command that reads from unsynchronized buffers!
    public void CommitPending()
    {
        if (pending.Count != 0)
        {
            FenceRecord rec = new();
            foreach (var (bufferId, ranges) in pending)
            {
                if (ranges.Count == 0) continue;

                rec.RangesByBuffer[bufferId] = ranges;
            }

            pending.Clear(); // Don't clear the transferred inner lists
            active.Enqueue(rec);
        }

        TrimSignaledFences(); // Prevent buildup
    }

    public void WaitForAll()
    {
        while (active.TryDequeue(out var rec))
        {
            GL.ClientWaitSync(rec.Fence, ClientWaitSyncFlags.SyncFlushCommandsBit, ulong.MaxValue);
            rec.Free();
        }
    }

    static ref ValueList<Range> GetOrCreateRanges(scoped ref ValueDictionary<int, ValueList<Range>> map, int bufferId)
    {
        ref var list = ref map.GetValueRefOrAddDefault(bufferId, out var found);
        if (found) return ref list;

        list = ValueList.Create<Range>();
        return ref list;
    }

    static void AddMerged(scoped ref ValueList<Range> list, Range r)
    {
        // Insert-and-merge to keep the list small (won't be a problem in the long run, is this necessary?)
        var i = 0;
        while (i < list.Count && list[i].End < r.Start) ++i;

        // Merge with any overlapping ranges
        var startIndex = i;
        while (i < list.Count && Range.Overlaps(list[i], r))
        {
            r.Start = Math.Min(r.Start, list[i].Start);
            r.End = Math.Max(r.End, list[i].End);
            ++i;
        }

        if (startIndex == i) list.Insert(i, r); // No overlap; insert at position i to roughly keep order
        else
        {
            // Replace [startIndex, i) with merged r
            list[startIndex] = r;
            list.RemoveRange(startIndex + 1, i - (startIndex + 1));
        }
    }

    static bool OverlapsAny(scoped ref readonly ValueList<Range> ranges, Range target)
    {
        foreach (var t in ranges)
            if (Range.Overlaps(t, target))
                return true;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsFenceSignaled(nint fence)
    {
        GL.GetSync(fence, SyncParameterName.SyncStatus, sizeof(int), out _, out var status);
        return status == 0x9119; // GL_SIGNALED = 0x9119
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void PopAndDeleteFrontFence() => active.Dequeue().Free();

    void TrimSignaledFences()
    {
        var count = active.Count;
        for (var i = 0; i < count; i++)
            if (IsFenceSignaled(active.Peek().Fence)) PopAndDeleteFrontFence();
            else break;

        // Stop at the first unsignaled fence; insertion order means later ones cannot be signaled earlier
        // in strict ordering, but drivers may complete out of order; still keep ordering for safety
    }

    struct Range
    {
        public int Start, End; // [Start, End)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Overlaps(Range a, Range b) => a.Start < b.End && b.Start < a.End;
    }

    struct FenceRecord()
    {
        public ValueDictionary<int, ValueList<Range>> RangesByBuffer = ValueDictionary.Create<int, ValueList<Range>>();
        public readonly nint Fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free()
        {
            GL.DeleteSync(Fence);

            foreach (var list in RangesByBuffer.Values) list.Dispose();
            RangesByBuffer.Dispose();
        }
    }
}