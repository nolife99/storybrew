namespace BrewLib.Util;

using System;
using System.Text;
using OpenTK.Windowing.GraphicsLibraryFramework;

public static unsafe class ClipboardHelper
{
    static object LastData;

    public static void SetText(ReadOnlySpan<char> text)
    {
        Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetByteCount(text)];
        Encoding.UTF8.GetBytes(text, bytes);

        fixed (byte* ptr = bytes) GLFW.SetClipboardStringRaw(Native.GLFWPtr, ptr);
    }
    public static string GetText() => GLFW.GetClipboardString(Native.GLFWPtr);

    public static void SetData(object data) => LastData = data;
    public static object GetData() => LastData;
}