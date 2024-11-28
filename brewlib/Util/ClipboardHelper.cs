namespace BrewLib.Util;

using System;
using System.Text;
using OpenTK.Windowing.GraphicsLibraryFramework;

public static class ClipboardHelper
{
    static object LastData;

    public static unsafe void SetText(ReadOnlySpan<char> text)
    {
        Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetByteCount(text)];
        Encoding.UTF8.GetBytes(text, bytes);

        fixed (byte* ptr = bytes) GLFW.SetClipboardStringRaw(Native.GLFWPtr, ptr);
    }
    public static unsafe string GetText() => GLFW.GetClipboardString(Native.GLFWPtr);

    public static void SetData(object data) => LastData = data;
    public static object GetData() => LastData;
}