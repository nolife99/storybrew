using OpenTK;
using System;
using System.Collections.Generic;
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

        public static void CopyMemory(IntPtr source, IntPtr destination, uint count)
        {
            if (source != default && destination != default) memcpy(destination, source, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)] 
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi)] 
        public static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowText(IntPtr hWnd)
        {
            var length = GetWindowTextLength(hWnd);
            if (length == 0) return "";

            var sb = new StringBuilder(length);
            GetWindowText(hWnd, sb, length + 1);
            return sb.ToString();
        }
        public static IEnumerable<IntPtr> EnumerateProcessWindowHandles(Process process)
        {
            var handles = new HashSet<IntPtr>();
            var threads = process.Threads;
            for (var i = 0; i < threads.Count; ++i) EnumThreadWindows(threads[i].Id, (hWnd, lParam) =>
            {
                handles.Add(hWnd);
                return true;
            }, IntPtr.Zero);

            return handles;
        }
        public static IntPtr FindProcessWindow(string title)
        {
            foreach (var hWnd in EnumerateProcessWindowHandles(Process.GetCurrentProcess())) if (GetWindowText(hWnd) == title)
                return hWnd;

            return IntPtr.Zero;
        }
        public static IntPtr GetWindowHandle(this GameWindow window)
        {
            var handle = FindProcessWindow(window.Title);
            if (handle != IntPtr.Zero) return handle;

            return window.WindowInfo.Handle;
        }
    }
}