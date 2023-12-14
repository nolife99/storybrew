using System;
using System.Collections.Generic;
using System.Threading;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics;

public sealed class GpuCommandSync : IDisposable
{
    List<SyncRange> syncRanges = [];

    public bool WaitForAll()
    {
        if (syncRanges.Count == 0) return false;

        var blocked = syncRanges[^1].Wait();

        for (var i = 0; i < syncRanges.Count; ++i) syncRanges[i].Dispose();
        syncRanges.Clear();

        return blocked;
    }
    public bool WaitForRange(int index, int length)
    {
        trimExpiredRanges();
        for (var i = syncRanges.Count - 1; i >= 0; i--)
        {
            var syncRange = syncRanges[i];
            if (index < syncRange.Index + syncRange.Length && syncRange.Index < index + length)
            {
                var blocked = syncRange.Wait();
                clearToIndex(i);
                return blocked;
            }
        }
        return false;
    }
    public void LockRange(int index, int length) => syncRanges.Add(new SyncRange(index, length));

    void trimExpiredRanges()
    {
        var left = 0;
        var right = syncRanges.Count - 1;

        var unblockedIndex = -1;
        while (left <= right)
        {
            var index = (left + right) / 2;
            var wouldBlock = syncRanges[index].Wait(false);
            if (wouldBlock) right = index - 1;
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

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            for (var i = 0; i < syncRanges.Count; ++i) syncRanges[i].Dispose();
            syncRanges.Clear();

            syncRanges = null;
            disposed = true;
        }
    }

    #endregion

    public static bool HasCapabilities() => DrawState.HasCapabilities(3, 2, "GL_ARB_sync");

    class SyncRange : IDisposable
    {
        internal int Index, Length;
        internal nint Fence;
        bool expired;

        internal SyncRange(int index, int length)
        {
            Index = index;
            Length = length;
            Fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
        }

        internal bool Wait(bool canBlock = true)
        {
            if (expired || Fence == 0) return false;

            var blocked = false;
            var waitSyncFlags = ClientWaitSyncFlags.None;
            var timeout = 0L;

            while (true)
            {
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
        }

        #region IDisposable Support

        bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                GL.DeleteSync(Fence);
                Fence = 0;
                disposed = true;
            }
        }
        ~SyncRange() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}