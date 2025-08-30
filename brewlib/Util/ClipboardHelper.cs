namespace BrewLib.Util;

using System;
using SDL3;

public static class ClipboardHelper
{
    static object LastData;

    public static void SetText(scoped ReadOnlySpan<char> text) => SDL.SetClipboardText(text.ToString());
    public static string GetText() => SDL.GetClipboardText();

    public static void SetData(object data) => LastData = data;
    public static object GetData() => LastData;
}