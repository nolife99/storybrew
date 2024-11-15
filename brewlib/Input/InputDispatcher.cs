namespace BrewLib.Input;

using System.Collections.Generic;
using OpenTK.Windowing.Common;

public sealed class InputDispatcher : InputHandler
{
    readonly List<InputHandler> handlers = [];

    public void OnFocusChanged(FocusedChangedEventArgs e)
    {
        foreach (var handler in handlers) handler.OnFocusChanged(e);
    }
    public bool OnClickDown(MouseButtonEventArgs e) => handlers.Find(h => h.OnClickDown(e)) is not null;
    public bool OnClickUp(MouseButtonEventArgs e) => handlers.Find(h => h.OnClickUp(e)) is not null;
    public bool OnMouseWheel(MouseWheelEventArgs e) => handlers.Find(h => h.OnMouseWheel(e)) is not null;
    public void OnMouseMove(MouseMoveEventArgs e)
    {
        foreach (var handler in handlers) handler.OnMouseMove(e);
    }
    public bool OnKeyDown(KeyboardKeyEventArgs e) => handlers.Find(h => h.OnKeyDown(e)) is not null;
    public bool OnKeyUp(KeyboardKeyEventArgs e) => handlers.Find(h => h.OnKeyUp(e)) is not null;
    public bool OnKeyPress(TextInputEventArgs e) => handlers.Find(h => h.OnKeyPress(e)) is not null;

    public void Add(InputHandler handler) => handlers.Add(handler);
    public void Remove(InputHandler handler) => handlers.Remove(handler);
}