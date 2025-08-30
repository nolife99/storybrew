namespace BrewLib.Input;

using System.Collections.Generic;
using SDL3;

public sealed class InputDispatcher : IInputHandler
{
    readonly List<IInputHandler> handlers = [];

    public void OnClose(SDL.QuitEvent e)
    {
        foreach (var handler in handlers) handler.OnClose(e);
    }

    public void OnResize(SDL.WindowEvent e)
    {
        foreach (var handler in handlers) handler.OnResize(e);
    }

    public void OnFocusChanged(SDL.WindowEvent e)
    {
        foreach (var handler in handlers) handler.OnFocusChanged(e);
    }

    public bool OnClickDown(SDL.MouseButtonEvent e)
    {
        foreach (var handler in handlers)
            if (handler.OnClickDown(e))
                return true;

        return false;
    }

    public bool OnClickUp(SDL.MouseButtonEvent e)
    {
        foreach (var handler in handlers)
            if (handler.OnClickUp(e))
                return true;

        return false;
    }

    public bool OnMouseWheel(SDL.MouseWheelEvent e)
    {
        foreach (var handler in handlers)
            if (handler.OnMouseWheel(e))
                return true;

        return false;
    }

    public void OnMouseMove(SDL.MouseMotionEvent e)
    {
        foreach (var handler in handlers) handler.OnMouseMove(e);
    }

    public bool OnKeyDown(SDL.KeyboardEvent e)
    {
        foreach (var handler in handlers)
            if (handler.OnKeyDown(e))
                return true;

        return false;
    }

    public bool OnKeyUp(SDL.KeyboardEvent e)
    {
        foreach (var handler in handlers)
            if (handler.OnKeyUp(e))
                return true;

        return false;
    }

    public bool OnKeyPress(SDL.TextInputEvent e)
    {
        foreach (var handler in handlers)
            if (handler.OnKeyPress(e))
                return true;

        return false;
    }

    public void Add(IInputHandler handler) => handlers.Add(handler);
    public void Remove(IInputHandler handler) => handlers.Remove(handler);
}