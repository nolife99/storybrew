namespace BrewLib.Input;

using OpenTK.Windowing.Common;

public abstract class InputAdapter : InputHandler
{
    public virtual void OnFocusChanged(FocusedChangedEventArgs e) { }
    public virtual bool OnClickDown(MouseButtonEventArgs e) => false;
    public virtual bool OnClickUp(MouseButtonEventArgs e) => false;
    public virtual bool OnMouseWheel(MouseWheelEventArgs e) => false;
    public virtual void OnMouseMove(MouseMoveEventArgs e) { }
    public virtual bool OnKeyDown(KeyboardKeyEventArgs e) => false;
    public virtual bool OnKeyUp(KeyboardKeyEventArgs e) => false;
    public virtual bool OnKeyPress(TextInputEventArgs e) => false;
}