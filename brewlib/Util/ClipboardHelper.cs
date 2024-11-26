namespace BrewLib.Util;

using System;
using System.Text;
using System.Windows;
using OpenTK.Windowing.GraphicsLibraryFramework;

public static unsafe class ClipboardHelper
{
    static int LastWriteSize;

    public static void SetText(ReadOnlySpan<char> text)
    {
        Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetByteCount(text)];
        Encoding.UTF8.GetBytes(text, bytes);

        fixed (byte* ptr = bytes) GLFW.SetClipboardStringRaw(Native.GLFWPtr, ptr);
    }
    public static string GetText() => GLFW.GetClipboardString(Native.GLFWPtr);

    public static void SetData(string format, object data) => Clipboard.SetData(format, data);
    public static object GetData(string format) => Clipboard.GetData(format);
}