using OpenTK;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace BrewLib.Util
{
    [SuppressUnmanagedCodeSecurity] public static class Native
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("kernel32.dll", EntryPoint = "RtlCopyMemory", CallingConvention = CallingConvention.Winapi)] 
        static extern void memcpy(IntPtr dest, IntPtr src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", CallingConvention = CallingConvention.Winapi)]
        static extern void memmove(IntPtr dest, IntPtr src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CopyMemory(IntPtr source, IntPtr destination, uint count)
        {
            if (source != default && destination != default)
            {
                if ((long)source < (long)destination + count && (long)source + count > (long)destination)
                {
                    memmove(destination, source, count);
                    return false;
                }
                memcpy(destination, source, count);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);

        public delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode)] 
        static unsafe extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)] 
        static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowText(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd);
            if (length == 0) return "";

            var sb = new StringBuilder(length);
            _ = GetWindowText(hWnd, sb, length + 1);
            return sb.ToString();
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