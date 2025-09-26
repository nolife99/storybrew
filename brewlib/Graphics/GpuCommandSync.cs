namespace BrewLib.Graphics;

using System;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public sealed class GpuCommandSync : IDisposable
{
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

    // Guard a written range for a buffer by the next CommitPending call: [offset, offset+size) in bytes
    public void LockRange(int bufferId, int offset, int size)
    {
        if (size <= 0) return;

        Range target = new(offset, offset + size);

        // If overlap with any pending range, force commit
        if (OverlapsPending(bufferId, target)) CommitPending();

        ref var ranges = ref GetOrCreateRanges(ref pending, bufferId);

        var count = ranges.Count;
        if (count == 0)
        {
            ranges.Add(target);
            return;
        }

        // Find first range with End >= newRange.Start
        int left = 0, right = count - 1;
        var insertIndex = count; // default: append at end
        while (left <= right)
        {
            var mid = right + left >> 1;
            if (ranges[mid].End.Value >= target.Start.Value)
            {
                insertIndex = mid;
                right = mid - 1;
            }
            else left = mid + 1;
        }

        // Merge overlapping/adjacent ranges starting from insertIndex
        var i = insertIndex;
        var start = target.Start.Value;
        var end = target.End.Value;

        while (i < count)
        {
            var r = ranges[i];
            if (r.Start.Value > end) break; // no overlap

            start = Math.Min(start, r.Start.Value);
            end = Math.Max(end, r.End.Value);
            i++;
        }

        var removeCount = i - insertIndex;
        if (removeCount > 0) ranges.RemoveRange(insertIndex, removeCount);

        ranges.Insert(insertIndex, new(start, end));
    }

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

        if (OverlapsPending(bufferId, target)) CommitPending();

        // Walk active fences in order, only wait on those that actually protect overlapping ranges for this buffer
        var count = active.Count;
        for (var i = 0; i < count; ++i)
        {
            var record = active.Dequeue();

            if (record.RangesByBuffer.TryGetValue(bufferId, out var ranges) &&
                OverlapsAny(ranges.AsReadOnlySpan(), target)) record.Free();

            // This fence doesn't guard this range, rotate it to the back
            else active.Enqueue(record);
        }
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

        FenceRecord rec = new();
        foreach (var (bufferId, ranges) in pending)
        {
            if (ranges.Count == 0) continue;

            rec.RangesByBuffer.TryAdd(bufferId, ranges);
        }

        pending.Clear(); // Don't clear the transferred inner lists
        active.Enqueue(rec);
    }

    public void WaitForAll()
    {
        while (active.TryDequeue(out var rec)) rec.Free();
    }

    static ref ValueList<Range> GetOrCreateRanges(scoped ref ValueDictionary<int, ValueList<Range>> map, int bufferId)
    {
        ref var list = ref map.GetValueRefOrAddDefault(bufferId, out var found);
        if (found) return ref list;

        list = ValueList.Create<Range>(4);
        return ref list;
    }

    static bool OverlapsAny(ReadOnlySpan<Range> ranges, Range target)
    {
        foreach (ref readonly var t in ranges)
            if (t.Start.Value < target.End.Value && target.Start.Value < t.End.Value)
                return true;

        return false;
    }

    bool OverlapsPending(int bufferId, Range target)
        => pending.TryGetValue(bufferId, out var ranges) && OverlapsAny(ranges.AsReadOnlySpan(), target);

    void TrimSignaledFences()
    {
        var count = active.Count;
        for (var i = 0; i < count; ++i)
            if (active.Peek().IsSignaled()) active.Dequeue().Free(false);
            else break;

        // Stop at the first unsignaled fence; insertion order means later ones cannot be signaled earlier
    }

    public static bool HasCapabilities()
        => DrawState.Extensions.Contains("GL_NV_fence") || DrawState.HasCapabilities(3, 2, "GL_ARB_sync");

    struct FenceRecord()
    {
        public ValueDictionary<int, ValueList<Range>> RangesByBuffer = ValueDictionary.Create<int, ValueList<Range>>(4);
        readonly nint fence = factory.Create();

        public readonly bool IsSignaled() => factory.IsSignaled(fence);

        public void Free(bool wait = true)
        {
            if (wait) factory.Wait(fence);
            factory.Delete(fence);

            foreach (var list in RangesByBuffer.Values) list.Dispose();
            RangesByBuffer.Dispose();
        }
    }

    readonly struct FenceFactory
    {
        public Func<nint> Create { get; init; }
        public Action<nint> Delete { get; init; }
        public Func<nint, bool> IsSignaled { get; init; }
        public Action<nint> Wait { get; init; }
    }
}