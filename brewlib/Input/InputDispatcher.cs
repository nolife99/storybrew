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
    public bool OnClickDown(MouseButtonEventArgs e) => handlers.Exists(h => h.OnClickDown(e));
    public bool OnClickUp(MouseButtonEventArgs e) => handlers.Exists(h => h.OnClickUp(e));
    public bool OnMouseWheel(MouseWheelEventArgs e) => handlers.Exists(h => h.OnMouseWheel(e));
    public void OnMouseMove(MouseMoveEventArgs e)
    {
        foreach (var handler in handlers) handler.OnMouseMove(e);
    }
    public bool OnKeyDown(KeyboardKeyEventArgs e) => handlers.Exists(h => h.OnKeyDown(e));
    public bool OnKeyUp(KeyboardKeyEventArgs e) => handlers.Exists(h => h.OnKeyUp(e));
    public bool OnKeyPress(TextInputEventArgs e) => handlers.Exists(h => h.OnKeyPress(e));

    public void Add(IInputHandler handler) => handlers.Add(handler);
    public void Remove(IInputHandler handler) => handlers.Remove(handler);
}