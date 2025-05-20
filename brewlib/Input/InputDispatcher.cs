namespace BrewLib.Input;

using System.Collections.Generic;
using OpenTK.Windowing.Common;

public sealed class InputDispatcher : IInputHandler
{
    readonly List<IInputHandler> handlers = [];

    public void OnFocusChanged(FocusedChangedEventArgs e)
    {
        foreach (var handler in handlers) handler.OnFocusChanged(e);
    }

    public bool OnClickDown(MouseButtonEventArgs e)
    {
        foreach (var handler in handlers)
            if (handler.OnClickDown(e))
                return true;
        return false;
    }
    public bool OnClickUp(MouseButtonEventArgs e)
    {
        foreach (var handler in handlers)
            if (handler.OnClickUp(e))
                return true;
        return false;
    }
    public bool OnMouseWheel(MouseWheelEventArgs e)
    {
        foreach (var handler in handlers)
            if (handler.OnMouseWheel(e))
                return true;
        return false;
    }

    public void OnMouseMove(MouseMoveEventArgs e)
    {
        foreach (var handler in handlers) handler.OnMouseMove(e);
    }

    public bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        foreach (var handler in handlers)
            if (handler.OnKeyDown(e))
                return true;
        return false;
    }
    public bool OnKeyUp(KeyboardKeyEventArgs e)
    {
        foreach (var handler in handlers)
            if (handler.OnKeyUp(e))
                return true;
        return false;
    }
    public bool OnKeyPress(TextInputEventArgs e)
    {
        foreach (var handler in handlers)
            if (handler.OnKeyPress(e))
                return true;
        return false;
    }

    public void Add(IInputHandler handler) => handlers.Add(handler);
    public void Remove(IInputHandler handler) => handlers.Remove(handler);
}