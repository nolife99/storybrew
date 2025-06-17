namespace BrewLib.Util;

using System;

public static class ClipboardHelper
{
    static object LastData;

    public static void SetText(scoped ReadOnlySpan<char> text) => Native.Window.ClipboardString = text.ToString();
    public static string GetText() => Native.Window.ClipboardString;

    public static void SetData(object data) => LastData = data;
    public static object GetData() => LastData;
}