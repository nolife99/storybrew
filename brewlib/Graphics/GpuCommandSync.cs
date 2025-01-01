namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Threading;
using Memory;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;

public static class GpuCommandSync
{
    static readonly Lazy<Pool<SyncRange>> syncRangePool = new(
        () => new(
            obj =>
            {
                GL.DeleteSync(obj.Fence);
                obj.Fence = 0;
                obj.Expired = false;
            }),
        LazyThreadSafetyMode.None);

    static readonly List<SyncRange> syncRanges = [];

    public static void DeleteFences()
    {
        foreach (var range in syncRanges) syncRangePool.Value.Release(range);
    }

    public static bool WaitForAll()
    {
        if (syncRanges.Count == 0) return false;

        var blocked = syncRanges[^1].Wait(true);

        foreach (var range in syncRanges) syncRangePool.Value.Release(range);
        syncRanges.Clear();

        return blocked;
    }

    public static bool WaitForRange(int index, int length)
    {
        trimExpiredRanges();
        for (var i = syncRanges.Count - 1; i >= 0; --i)
        {
            var syncRange = syncRanges[i];
            if (index >= syncRange.Index + syncRange.Length || syncRange.Index >= index + length) continue;

            var blocked = syncRange.Wait(true);
            clearToIndex(i);
            return blocked;
        }

        return false;
    }

    public static void LockRange(int index, int length)
    {
        var item = syncRangePool.Value.Retrieve();

        item.Fence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
        item.Index = index;
        item.Length = length;
        syncRanges.Add(item);
    }

    static void trimExpiredRanges()
    {
        var left = 0;
        var right = syncRanges.Count - 1;

        var unblockedIndex = -1;
        while (left <= right)
        {
            var index = (left + right) / 2;
            if (syncRanges[index].Wait(false)) right = index - 1;
            else
            {
                left = index + 1;
                unblockedIndex = int.Max(unblockedIndex, index);
            }
        }

        if (unblockedIndex >= 0) clearToIndex(unblockedIndex);
    }

    static void clearToIndex(int index)
    {
        for (var i = 0; i <= index; ++i) syncRangePool.Value.Release(syncRanges[i]);
        syncRanges.RemoveRange(0, index + 1);
    }

    public static bool HasCapabilities() => GLFW.ExtensionSupported("GL_ARB_sync");

    sealed class SyncRange
    {
        public bool Expired;
        public nint Fence;
        public int Index, Length;

        public bool Wait(bool canBlock)
        {
            if (Expired || Fence == 0) return false;

            var blocked = false;
            var waitSyncFlags = ClientWaitSyncFlags.None;
            var timeout = 0L;

            while (true)
                switch (GL.ClientWaitSync(Fence, waitSyncFlags, timeout))
                {
                    case WaitSyncStatus.AlreadySignaled:
                        Expired = true;
                        return blocked;

                    case WaitSyncStatus.ConditionSatisfied:
                        Expired = true;
                        return true;

                    case WaitSyncStatus.TimeoutExpired:
                        if (!canBlock) return true;

                        blocked = true;
                        waitSyncFlags = ClientWaitSyncFlags.SyncFlushCommandsBit;
                        timeout = 1000000000L;
                    break;

                    case WaitSyncStatus.WaitFailed: throw new SynchronizationLockException("ClientWaitSync failed");
                }
        }
    }
}