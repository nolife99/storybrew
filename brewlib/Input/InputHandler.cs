namespace BrewLib.Input;

using OpenTK.Windowing.Common;

public interface InputHandler
{
    void OnFocusChanged(FocusedChangedEventArgs e);
    bool OnClickDown(MouseButtonEventArgs e);
    bool OnClickUp(MouseButtonEventArgs e);
    bool OnMouseWheel(MouseWheelEventArgs e);
    void OnMouseMove(MouseMoveEventArgs e);
    bool OnKeyDown(KeyboardKeyEventArgs e);
    bool OnKeyUp(KeyboardKeyEventArgs e);
    bool OnKeyPress(TextInputEventArgs e);
}