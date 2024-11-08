namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Threading;
using osuTK.Graphics.OpenGL;

public sealed class GpuCommandSync : IDisposable
{
    readonly List<SyncRange> syncRanges = [];

    public bool WaitForAll()
    {
        if (syncRanges.Count == 0) return false;

        var blocked = syncRanges[^1].Wait();

        foreach (var range in syncRanges) range.Dispose();
        syncRanges.Clear();

        return blocked;
    }

    public bool WaitForRange(int index, int length)
    {
        trimExpiredRanges();
        for (var i = syncRanges.Count - 1; i >= 0; --i)
        {
            var syncRange = syncRanges[i];
            if (index >= syncRange.Index + syncRange.Length || syncRange.Index >= index + length) continue;
            var blocked = syncRange.Wait();
            clearToIndex(i);
            return blocked;
        }

        return false;
    }

    public void LockRange(int index, int length) => syncRanges.Add(new(index, length));

    void trimExpiredRanges()
    {
        var left = 0;
        var right = syncRanges.Count - 1;

        var unblockedIndex = -1;
        while (left <= right)
        {
            var index = (left + right) / 2;
            var wouldBlock = syncRanges[index].Wait(false);
            if (wouldBlock)
                right = index - 1;
            else
            {
                left = index + 1;
                unblockedIndex = Math.Max(unblockedIndex, index);
            }
        }

        if (unblockedIndex >= 0) clearToIndex(unblockedIndex);
    }

    void clearToIndex(int index)
    {
        for (var i = 0; i <= index; ++i) syncRanges[i].Dispose();
        syncRanges.RemoveRange(0, index + 1);
    }

    public static bool HasCapabilities() => DrawState.HasCapabilities(3, 2, "GL_ARB_sync");

    sealed class SyncRange(int index, int length) : IDisposable
    {
        public readonly int Index = index, Length = length;
        bool expired;
        nint Fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);

        public bool Wait(bool canBlock = true)
        {
            if (expired || Fence == 0) return false;

            var blocked = false;
            var waitSyncFlags = ClientWaitSyncFlags.None;
            var timeout = 0;

            while (true)
                switch (GL.ClientWaitSync(Fence, waitSyncFlags, timeout))
                {
                    case WaitSyncStatus.AlreadySignaled:
                        expired = true;
                        return blocked;

                    case WaitSyncStatus.ConditionSatisfied:
                        expired = true;
                        return true;

                    case WaitSyncStatus.WaitFailed: throw new SynchronizationLockException("ClientWaitSync failed");
                    case WaitSyncStatus.TimeoutExpired:
                        if (!canBlock) return true;
                        blocked = true;
                        waitSyncFlags = ClientWaitSyncFlags.SyncFlushCommandsBit;
                        timeout = 1000000000;
                        break;
                }
        }

#region IDisposable Support

        bool disposed;

        void Dispose(bool disposing)
        {
            if (disposed) return;
            GL.DeleteSync(Fence);

            if (!disposing) return;
            Fence = 0;
            disposed = true;
        }

        ~SyncRange() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

#endregion
    }

#region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        foreach (var range in syncRanges) range.Dispose();
        syncRanges.Clear();

        disposed = true;
    }

#endregion
}