using osuTK;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace BrewLib.Util
{
    [SuppressUnmanagedCodeSecurity] public static class Native
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("kernel32.dll", EntryPoint = "RtlCopyMemory", CallingConvention = CallingConvention.Winapi)] 
        static extern void memcpy(nint dest, nint src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", CallingConvention = CallingConvention.Winapi)]
        static extern void memmove(nint dest, nint src, uint count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CopyMemory(nint source, nint destination, uint count)
        {
            if (source != default && destination != default)
            {
                if (source < destination + count && source + count > destination)
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
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadWndProc lpfn, nint lParam);

        public delegate bool EnumThreadWndProc(nint hWnd, nint lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Ansi)] 
        static unsafe extern int GetWindowText(nint hWnd, byte* lpString, int nMaxCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)] 
        static extern int GetWindowTextLength(nint hWnd);

        public unsafe static string GetWindowText(nint hWnd)
        {
            var length = GetWindowTextLength(hWnd) + 1;
            if (length == 1) return "";

            var buffer = stackalloc byte[length * 2];
            _ = GetWindowText(hWnd, buffer, length);
            return new string((sbyte*)buffer);
        }
        public static nint FindProcessWindow(string title)
        {
            var handle = nint.Zero;
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
        public static nint GetWindowHandle(this GameWindow window)
        {
            var handle = FindProcessWindow(window.Title);
            if (handle != default) return handle;

            return window.WindowInfo.Handle;
        }
    }
}