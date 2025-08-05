namespace BrewLib.Graphics;

using System;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;

sealed class GpuCommandSync : IDisposable
{
    readonly PooledList<SyncRange> syncRanges = new();

    public void Dispose()
    {
        foreach (var range in syncRanges) range.Dispose();
        syncRanges.Dispose();
    }

    public bool WaitForAll()
    {
        if (syncRanges.Count == 0) return false;

        var blocked = syncRanges[^1].Wait(true);

        foreach (var range in syncRanges) range.Dispose();
        syncRanges.Clear();

        return blocked;
    }

    public bool WaitForRange(nint index, int length)
    {
        trimExpiredRanges();

        var syncRangeSpan = syncRanges.AsSpan();
        for (var i = syncRangeSpan.Length - 1; i >= 0; --i)
        {
            ref var syncRange = ref syncRangeSpan[i];
            if (index >= syncRange.Index + syncRange.Length || syncRange.Index >= index + length) continue;

            var blocked = syncRange.Wait(true);
            clearToIndex(i);
            return blocked;
        }

        return false;
    }

    public void LockRange(nint index, int length)
    {
        SyncRange item = new() { Fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None) };

        GL.Flush();

        item.Index = index;
        item.Length = length;
        syncRanges.Add(item);
    }

    void trimExpiredRanges()
    {
        var syncRangeSpan = syncRanges.AsSpan();

        var left = 0;
        var right = syncRangeSpan.Length - 1;

        var unblockedIndex = -1;
        while (left <= right)
        {
            var index = right + left >> 1;
            if (syncRangeSpan[index].Wait(false)) right = index - 1;
            else
            {
                left = index + 1;
                unblockedIndex = int.Max(unblockedIndex, index);
            }
        }

        if (unblockedIndex >= 0) clearToIndex(unblockedIndex);
    }

    void clearToIndex(int index)
    {
        for (var i = 0; i <= index; ++i) syncRanges[i].Dispose();
        syncRanges.RemoveRange(0, index + 1);
    }

    struct SyncRange : IDisposable
    {
        public bool Expired;
        public nint Fence, Index;
        public int Length;

        public bool Wait(bool canBlock)
        {
            if (Expired || Fence == 0) return false;

            if (!canBlock)
            {
                GL.GetSync(Fence, SyncParameterName.SyncStatus, sizeof(int), out _, out var values);
                var unsignaled = values == 0x9118;
                if (!unsignaled) Expired = true;

                return unsignaled;
            }

            var blocked = false;
            ulong timeout = 0;

            while (true)
                switch (GL.ClientWaitSync(Fence, ClientWaitSyncFlags.None, timeout))
                {
                    case WaitSyncStatus.AlreadySignaled:
                        Expired = true;
                        return blocked;

                    case WaitSyncStatus.ConditionSatisfied:
                        Expired = true;
                        return true;

                    case WaitSyncStatus.TimeoutExpired:
                        blocked = true;
                        timeout = 1000000000L;
                        break;

                    case WaitSyncStatus.WaitFailed: throw new InvalidOperationException("ClientWaitSync failed");
                }
        }

        public void Dispose() => GL.DeleteSync(Fence);
    }
}