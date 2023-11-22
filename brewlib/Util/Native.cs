using OpenTK;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace BrewLib.Util
{
    [SuppressUnmanagedCodeSecurity] public static class Native
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)] 
        static extern void RtlCopyMemory(IntPtr dest, IntPtr src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern void RtlMoveMemory(IntPtr dest, IntPtr src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CopyMemory(IntPtr source, IntPtr destination, int count)
        {
            if (Environment.Is64BitProcess)
            {
                if (source != default && destination != default)
                {
                    if ((long)source < (long)destination + count && (long)source + count > (long)destination)
                    {
                        RtlMoveMemory(destination, source, (uint)count);
                        return false;
                    }
                    RtlCopyMemory(destination, source, (uint)count);
                    return true;
                }
                return false;
            }
            unsafe
            {
                Buffer.MemoryCopy(source.ToPointer(), destination.ToPointer(), count, count);
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        public delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi)] 
        static unsafe extern int GetWindowText(IntPtr hWnd, IntPtr lpString, int nMaxCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)] 
        static extern int GetWindowTextLength(IntPtr hWnd);

        public unsafe static string GetWindowText(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd);
            if (length == 0) return "";

            var buffer = Marshal.AllocCoTaskMem(length * 2 + 1);
            _ = GetWindowText(hWnd, buffer, length);

            var result = Marshal.PtrToStringAnsi(buffer);
            Marshal.FreeCoTaskMem(buffer);

            return result;
        }
        public static IntPtr FindProcessWindow(string title)
        {
            var handle = IntPtr.Zero;
            var cont = true;
            var threads = Process.GetCurrentProcess().Threads;

            for (var i = 0; i < threads.Count && cont; ++i) EnumThreadWindows(threads[i].Id, (hWnd, lParam) =>
            {
                if (GetWindowText(hWnd) == title)
                {
                    handle = hWnd;
                    cont = false;
                }
                return cont;
            }, default);

            return handle;
        }
        public static IntPtr GetWindowHandle(this GameWindow window)
        {
            var handle = FindProcessWindow(window.Title);
            if (handle != default) return handle;

            return window.WindowInfo.Handle;
        }
    }
}