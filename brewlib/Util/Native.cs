namespace BrewLib.Util;

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.IO;
using Microsoft.Win32.SafeHandles;
using SDL3;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using Image = SixLabors.ImageSharp.Image;

public static partial class Native
{
    static nint windowHandle;

    public static Func<Action<object>, object, ValueTask> MainThreadScheduler { get; set; }

    public static void InitializeHandle(nint window) => windowHandle = window;

    public static Task SetWindowIcon(ResourceContainer container, string iconPath)
    {
        var r = container.GetStream(iconPath, ResourceSource.Embedded);
        if (r is null) return Task.CompletedTask;

        return Task.Factory.StartNew(async streamObj =>
            {
                await using var iconResource = (Stream)streamObj;
                using var image = await Image.LoadAsync<Rgba32>(iconResource);
                image.Mutate(x => x.Resize(new(48), KnownResamplers.Triangle, false));

                using var bytes = ValueArray.Create<byte>(image.Width * image.Height * Unsafe.SizeOf<Rgba32>());
                image.CopyPixelDataTo(bytes.AsSpan());

                bytes.GetUnsafe(out var array, out _);

                var pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
                var surface = SDL.CreateSurfaceFrom(image.Width,
                    image.Height,
                    SDL.PixelFormat.ABGR8888,
                    pinned.AddrOfPinnedObject(),
                    image.Width * 4);

                SDL.RunOnMainThread(x => SDL.SetWindowIcon(windowHandle, x), surface, true);

                SDL.DestroySurface(surface);
                pinned.Free();
            },
            r);
    }

    #region Win32

    static readonly bool SupportsHighResTimer = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393);

    [ThreadStatic]
    static EventWaitHandle perThreadTimer;

    const string KERNEL32 = "kernel32.dll";

    [LibraryImport(KERNEL32, SetLastError = true), MethodImpl(MethodImplOptions.AggressiveInlining),
     SuppressGCTransition]
    private static partial SafeWaitHandle CreateWaitableTimerExW(nint lpTimerAttributes,
        nint lpTimerName,
        uint dwFlags,
        uint dwDesiredAccess);

    [LibraryImport(KERNEL32, SetLastError = true), MethodImpl(MethodImplOptions.AggressiveInlining),
     SuppressGCTransition]
    private static partial int SetWaitableTimer(SafeWaitHandle hTimer,
        nint pDueTime,
        int lPeriod,
        nint pfnCompletionRoutine,
        nint lpArgToCompletionRoutine,
        int fResume);

    public static void AccurateSleep(long ticks)
    {
        if (!SupportsHighResTimer)
        {
            SDL.DelayNS((ulong)(ticks * TimeSpan.NanosecondsPerTick));
            return;
        }

        var timer = perThreadTimer;
        if (timer is null)
        {
            const uint CREATE_WAITABLE_TIMER_MANUAL_RESET = 0x00000001,
                CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002, TIMER_ALL_ACCESS = 0x1F0003;

            timer = perThreadTimer = new(false, EventResetMode.AutoReset);

            timer.SafeWaitHandle.Dispose();
            timer.SafeWaitHandle = CreateWaitableTimerExW(0,
                0,
                CREATE_WAITABLE_TIMER_MANUAL_RESET | CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
                TIMER_ALL_ACCESS);
        }

        var relativeTicks = -ticks + TimeSpan.TicksPerMillisecond / 4;
        if (SetWaitableTimer(timer.SafeWaitHandle, relativeTicks.AsPointer(), 0, 0, 0, 0) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        timer.WaitOne();
    }

    #endregion
}