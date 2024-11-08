namespace BrewLib.Input;

using System.Collections.Generic;
using System.Linq;
using osuTK;
using osuTK.Input;

public class InputDispatcher : InputHandler
{
    readonly List<InputHandler> handlers;
    public InputDispatcher() => handlers = [];

    public void OnFocusChanged(FocusChangedEventArgs e) => handlers.ForEach(handler => handler.OnFocusChanged(e));
    public bool OnClickDown(MouseButtonEventArgs e) => handlers.Any(h => h.OnClickDown(e));
    public bool OnClickUp(MouseButtonEventArgs e) => handlers.Any(h => h.OnClickUp(e));
    public bool OnMouseWheel(MouseWheelEventArgs e) => handlers.Any(h => h.OnMouseWheel(e));
    public void OnMouseMove(MouseMoveEventArgs e) => handlers.ForEach(handler => handler.OnMouseMove(e));
    public bool OnKeyDown(KeyboardKeyEventArgs e) => handlers.Any(h => h.OnKeyDown(e));
    public bool OnKeyUp(KeyboardKeyEventArgs e) => handlers.Any(h => h.OnKeyUp(e));
    public bool OnKeyPress(KeyPressEventArgs e) => handlers.Any(h => h.OnKeyPress(e));

    public virtual void OnGamepadConnected(GamepadEventArgs e)
        => handlers.ForEach(handler => handler.OnGamepadConnected(e));

    public virtual bool OnGamepadButtonDown(GamepadButtonEventArgs e) => handlers.Any(h => h.OnGamepadButtonDown(e));
    public virtual bool OnGamepadButtonUp(GamepadButtonEventArgs e) => handlers.Any(h => h.OnGamepadButtonUp(e));

    public void Add(InputHandler handler) => handlers.Add(handler);
    public void Remove(InputHandler handler) => handlers.Remove(handler);
}