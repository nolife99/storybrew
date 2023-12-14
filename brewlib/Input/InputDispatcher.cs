using System.Collections.Generic;
using osuTK;
using osuTK.Input;

namespace BrewLib.Input;

public class InputDispatcher : InputHandler
{
    readonly List<InputHandler> handlers;
    public InputDispatcher() => handlers = [];

    public void Add(InputHandler handler) => handlers.Add(handler);
    public void Remove(InputHandler handler) => handlers.Remove(handler);
    public void Clear() => handlers.Clear();

    public void OnFocusChanged(FocusChangedEventArgs e) => handlers.ForEach(handler => handler.OnFocusChanged(e));
    public bool OnClickDown(MouseButtonEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnClickDown(e)) return true;
        return false;
    }
    public bool OnClickUp(MouseButtonEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnClickUp(e)) return true;
        return false;
    }
    public bool OnMouseWheel(MouseWheelEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnMouseWheel(e)) return true;
        return false;
    }
    public void OnMouseMove(MouseMoveEventArgs e) => handlers.ForEach(handler => handler.OnMouseMove(e));
    public bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnKeyDown(e)) return true;
        return false;
    }
    public bool OnKeyUp(KeyboardKeyEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnKeyUp(e)) return true;
        return false;
    }
    public bool OnKeyPress(KeyPressEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnKeyPress(e)) return true;
        return false;
    }
    public virtual void OnGamepadConnected(GamepadEventArgs e) => handlers.ForEach(handler => handler.OnGamepadConnected(e));
    public virtual bool OnGamepadButtonDown(GamepadButtonEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnGamepadButtonDown(e)) return true;
        return false;
    }
    public virtual bool OnGamepadButtonUp(GamepadButtonEventArgs e)
    {
        for (var i = 0; i < handlers.Count; ++i) if (handlers[i].OnGamepadButtonUp(e)) return true;
        return false;
    }
}