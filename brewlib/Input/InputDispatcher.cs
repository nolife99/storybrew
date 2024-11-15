namespace BrewLib.Input;

using System.Collections.Generic;
using System.Linq;
using OpenTK.Windowing.Common;

public sealed class InputDispatcher : InputHandler
{
    readonly List<InputHandler> handlers = [];

    public void OnFocusChanged(FocusedChangedEventArgs e)
    {
        foreach (var handler in handlers) handler.OnFocusChanged(e);
    }
    public bool OnClickDown(MouseButtonEventArgs e) => handlers.Any(h => h.OnClickDown(e));
    public bool OnClickUp(MouseButtonEventArgs e) => handlers.Any(h => h.OnClickUp(e));
    public bool OnMouseWheel(MouseWheelEventArgs e) => handlers.Any(h => h.OnMouseWheel(e));
    public void OnMouseMove(MouseMoveEventArgs e)
    {
        foreach (var handler in handlers) handler.OnMouseMove(e);
    }
    public bool OnKeyDown(KeyboardKeyEventArgs e) => handlers.Any(h => h.OnKeyDown(e));
    public bool OnKeyUp(KeyboardKeyEventArgs e) => handlers.Any(h => h.OnKeyUp(e));
    public bool OnKeyPress(TextInputEventArgs e) => handlers.Any(h => h.OnKeyPress(e));

    public void Add(InputHandler handler) => handlers.Add(handler);
    public void Remove(InputHandler handler) => handlers.Remove(handler);
}