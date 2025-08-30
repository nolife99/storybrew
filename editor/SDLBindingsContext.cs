namespace StorybrewEditor;

using System.Runtime.InteropServices;
using OpenTK;
using SDL3;

public class SDLBindingsContext : IBindingsContext
{
    public nint GetProcAddress(string procName)
    {
        var procNamePtr = SDL.GLGetProcAddress(procName);
        return procNamePtr is null ? 0 : Marshal.GetFunctionPointerForDelegate(procNamePtr);
    }
}