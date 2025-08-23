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
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using Image = SixLabors.ImageSharp.Image;

public static class Native
{
    public static NativeWindow Window { get; private set; }

    public static Func<Action<object>, object, ValueTask> MainThreadScheduler { get; set; }

    public static void InitializeHandle(NativeWindow glfwWindow) => Window = glfwWindow;

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

                await MainThreadScheduler(x => Window.Icon = (WindowIcon)x,
                    new WindowIcon(new OpenTK.Windowing.Common.Input.Image(image.Width, image.Height, array)));
            },
            r);
    }

    #region Win32

    static readonly bool SupportsHighResTimer = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393);

    [ThreadStatic] static SafeWaitHandle perThreadTimer;

    const string KERNEL32 = "kernel32.dll";

    [DllImport(KERNEL32, SetLastError = true), SuppressGCTransition]
    static extern nint CreateWaitableTimerExW(nint lpTimerAttributes,
        nint lpTimerName,
        uint dwFlags,
        uint dwDesiredAccess);

    [DllImport(KERNEL32, SetLastError = true), SuppressGCTransition]
    static extern int SetWaitableTimer(nint hTimer,
        nint pDueTime,
        int lPeriod,
        nint pfnCompletionRoutine,
        nint lpArgToCompletionRoutine,
        int fResume);

    [DllImport(KERNEL32, SetLastError = true)]
    static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    public static void AccurateSleep(long ticks)
    {
        if (!SupportsHighResTimer)
        {
            Thread.Sleep((int)(ticks / TimeSpan.TicksPerMillisecond));
            return;
        }

        var timer = perThreadTimer;
        if (timer is null)
        {
            const uint CREATE_WAITABLE_TIMER_MANUAL_RESET = 0x00000001,
                CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002, TIMER_ALL_ACCESS = 0x1F0003;

            var handle = CreateWaitableTimerExW(0,
                0,
                CREATE_WAITABLE_TIMER_MANUAL_RESET | CREATE_WAITABLE_TIMER_HIGH_RESOLUTION,
                TIMER_ALL_ACCESS);

            if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

            timer = perThreadTimer = new(handle, true);
        }

        var relativeTicks = -ticks + TimeSpan.TicksPerMillisecond / 2;
        var timerHandle = timer.DangerousGetHandle();

        if (SetWaitableTimer(timerHandle, relativeTicks.AsPointer(), 0, 0, 0, 0) == 0 ||
            WaitForSingleObject(timerHandle, uint.MaxValue) == 0xFFFFFFFF)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    #endregion
}