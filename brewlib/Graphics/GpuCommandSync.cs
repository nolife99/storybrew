namespace BrewLib.Graphics;

using System;
using System.Buffers;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public sealed class GpuCommandSync : IDisposable
{
    // TODO: Fix abysmal performance when lots of fences

    static readonly FenceFactory factory = DrawState.Extensions.Contains("GL_NV_fence") ?
        new()
        {
            Create = () =>
            {
                var h = GL.NV.GenFence();
                GL.NV.SetFence(h, FenceConditionNv.AllCompletedNv);
                return h;
            },
            Delete = h => GL.NV.DeleteFence(unchecked((int)h)),
            IsSignaled = h => GL.NV.TestFence(unchecked((int)h)),
            Wait = h => GL.NV.FinishFence(unchecked((int)h))
        } :
        new()
        {
            Create = () =>
            {
                var h = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
                GL.Flush();
                return h;
            },
            Delete = GL.DeleteSync,
            IsSignaled = h =>
            {
                GL.GetSync(h, SyncParameterName.SyncStatus, 1, out _, out var status);
                return status == (int)All.Signaled;
            },
            Wait = h => GL.ClientWaitSync(h, ClientWaitSyncFlags.None, ulong.MaxValue)
        };

    readonly FlatTriplesBuffer tripleBuffer = new(2048);

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
        tripleBuffer.Dispose();
    }

    // Utility method for less boilerplate
    public void WaitAndLockRange(int bufferId, int offset, int size)
    {
        WaitForRange(bufferId, offset, size);
        LockRange(bufferId, offset, size);
    }

    public void WaitAndLockRangeWrap(int bufferId, int offset, int size, int bufferSize)
    {
        WaitForRangeWrap(bufferId, offset, size, bufferSize);
        LockRangeWrap(bufferId, offset, size, bufferSize);
    }

    // Guard a written range for a buffer by the next CommitPending call: <[offset, offset+size) in bytes>
    public void LockRange(int bufferId, int offset, int size)
        => GetOrCreateRanges(bufferId).Add(new(offset, offset + size));

    // Extension for ring buffers where a write may wrap around
    public void LockRangeWrap(int bufferId, int offset, int size, int bufferSize)
    {
        if (size <= 0) return;

        if (offset + size <= bufferSize) LockRange(bufferId, offset, size);
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

        Range target = new(offset, offset + size);

        var count = active.Count;
        for (var i = 0; i < count; ++i)
        {
            var rec = active.Dequeue();

            if (rec.HasOverlap(tripleBuffer, bufferId, target))
            {
                rec.WaitAndReleaseFence();
                continue;
            }

            active.Enqueue(rec);
        }
    }

    void ReclaimPrefixFreedTriples()
    {
        // Walk from the head of the active queue and compute minimal StartIndex among remaining fences
        // [Tail..minStartIndex) is fully free
        if (active.Count == 0)
        {
            // Everything freed -> advance tail to head
            tripleBuffer.AdvanceTail(tripleBuffer.Head);
            return;
        }

        // Find smallest StartIndex of fences still in the queue (they are ordered by insertion, so tail of queue
        // corresponds to oldest fence; however fences may be freed out-of-order - we freed them above by not re-enqueue)
        var minStart = int.MaxValue;
        var n = active.Count;

        for (var i = 0; i < n; ++i)
        {
            var rec = active.Dequeue();
            if (rec.StartIndex < minStart) minStart = rec.StartIndex;
            active.Enqueue(rec);
        }

        if (minStart == int.MaxValue) return;

        // Advance tail to minStart
        tripleBuffer.AdvanceTail(minStart);
    }

    // For ring buffers where a wait range may wrap around
    public void WaitForRangeWrap(int bufferId, int offset, int size, int bufferSize)
    {
        if (size <= 0) return;

        if (offset + size <= bufferSize) WaitForRange(bufferId, offset, size);
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
        TrimSignaledFences();

        if (pending.Count == 0) return;

        var totalRanges = 0;
        foreach (var ranges in pending.Values) totalRanges += ranges.Count;
        if (totalRanges == 0) return;

        var totalInts = totalRanges * 3;

        int[] rented = null;
        scoped Span<int> tempSpan;
        if (totalInts <= 256)
        {
            Span<int> stackSpan = stackalloc int[totalInts];
            tempSpan = stackSpan;
        }
        else
        {
            rented = ArrayPool<int>.Shared.Rent(totalInts);
            tempSpan = new(rented, 0, totalInts);
        }

        var pos = 0;
        foreach (var (bufferId, ranges) in pending)
        foreach (var r in ranges.AsReadOnlySpan())
        {
            tempSpan[pos++] = bufferId;
            tempSpan[pos++] = r.Start.Value;
            tempSpan[pos++] = r.End.Value;
        }

        // Clear pending inner lists (dispose so their buffers return)
        foreach (var ranges in pending.Values) ranges.Dispose();
        pending.Clear();

        // Append to the shared buffer — returns start index within the shared buffer
        var (startIndex, count) = tripleBuffer.AppendTriples(tempSpan);

        if (rented is not null) ArrayPool<int>.Shared.Return(rented);

        active.Enqueue(new(startIndex, count, factory));
    }

    public void WaitForAll()
    {
        while (active.TryDequeue(out var rec)) rec.WaitAndReleaseFence();

        // Reclaim everything
        tripleBuffer.AdvanceTail(tripleBuffer.Head);
    }

    ref ValueList<Range> GetOrCreateRanges(int bufferId)
    {
        ref var list = ref pending.GetValueRefOrAddDefault(bufferId, out var found);
        if (found) return ref list;

        list = ValueList.Create<Range>(4);
        return ref list;
    }

    void TrimSignaledFences()
    {
        var anyFreed = false;

        // Pop signaled fences from the front
        while (active.Count > 0 && active.Peek().IsSignaled())
        {
            active.Dequeue().WaitAndReleaseFence();
            anyFreed = true;
        }

        if (anyFreed) ReclaimPrefixFreedTriples();
    }

    public static bool HasCapabilities()
        => DrawState.Extensions.Contains("GL_NV_fence") || DrawState.Extensions.Contains("GL_ARB_sync");
}

readonly struct FenceRecord(int startIndex, int count, FenceFactory factory)
{
    readonly nint fence = factory.Create();
    public readonly int StartIndex = startIndex; // index into FlatTriplesBuffer

    public bool IsSignaled() => factory.IsSignaled(fence);

    public void WaitAndReleaseFence()
    {
        // Wait and delete fence as before
        factory.Wait(fence);
        factory.Delete(fence);

        // Mark its triples as free by advancing a 'freedTail' marker externally
        // The sharedBuffer tail advancement happens in the caller after we know which
        // contiguous prefix of fences have been freed
    }

    // Scan the shared buffer slice
    public bool HasOverlap(FlatTriplesBuffer buf, int bufferId, Range target)
    {
        var end = StartIndex + count;
        for (var i = StartIndex; i < end; i += 3)
        {
            if (buf[i] != bufferId) continue;

            var s = buf[i + 1];
            var e = buf[i + 2];
            if (s < target.End.Value && target.Start.Value < e) return true;
        }

        return false;
    }
}

readonly struct FenceFactory
{
    public Func<nint> Create { get; init; }
    public Action<nint> Delete { get; init; }
    public Func<nint, bool> IsSignaled { get; init; }
    public Action<nint> Wait { get; init; }
}

sealed class FlatTriplesBuffer
{
    readonly ArrayPool<int> pool;

    int[] buffer;
    int capacity;

    public FlatTriplesBuffer(int initialCapacity = 1024)
    {
        pool = ArrayPool<int>.Shared;
        buffer = pool.Rent(initialCapacity);
        capacity = buffer.Length;
        Head = 0;
        Tail = 0;
    }

    public int Head { get; private set; } // next write index
    public int Tail { get; private set; } // oldest live index (we reclaim from tail up to head)

    // Access element at index (absolute index)
    public int this[int index] => buffer[index];

    public void Dispose()
    {
        if (buffer is not null)
        {
            pool.Return(buffer);
            buffer = null!;
            capacity = 0;
            Head = Tail = 0;
        }
    }

    void EnsureCapacityForAppend(int count)
    {
        var freeSpace = capacity - Head;
        if (freeSpace >= count) return;

        var newCap = capacity * 2;
        while (newCap - Head < count) newCap *= 2;

        var newBuf = pool.Rent(newCap);

        // Copy valid region [Tail .. Head) into newBuf at index 0
        var live = Head - Tail;
        if (live > 0) Array.Copy(buffer, Tail, newBuf, 0, live);

        pool.Return(buffer);

        buffer = newBuf;
        capacity = newCap;
        Head = live;
        Tail = 0;
    }

    // Append triples from a ReadOnlySpan<int> (MUST be multiple of 3). Returns start index and count appended.
    public (int startIndex, int count) AppendTriples(scoped ReadOnlySpan<int> triples)
    {
        if (triples.Length == 0) return (0, 0);

        EnsureCapacityForAppend(triples.Length);
        var start = Head;
        triples.CopyTo(new(buffer, Head, triples.Length));
        Head += triples.Length;
        return (start, triples.Length);
    }

    // Try to advance tail if the freed region from 'tailAdvanceTo' is at the current tail
    // We expect the caller to call AdvanceTail() when certain fences free
    // tailAdvanceTo is an index position that we can advance Tail to. This method will
    // compress/shift live data to index 0 if Tail becomes large
    public void AdvanceTail(int newTail)
    {
        if (newTail <= Tail) return;

        if (newTail > Head) newTail = Head; // clamp

        Tail = newTail;

        // If tail is large relative to capacity, slide down live range to 0 to free space at the end
        // This avoids unbounded Head growth when many small frees happen.
        if (Tail > capacity / 2)
        {
            var live = Head - Tail;
            if (live > 0) Array.Copy(buffer, Tail, buffer, 0, live);
            Head = live;
            Tail = 0;
        }
    }
}