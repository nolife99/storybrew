namespace BrewLib.Input;

using SDL3;

public abstract class InputAdapter : IInputHandler
{
    public virtual void OnClose(SDL.QuitEvent e) { }
    public virtual void OnResize(SDL.WindowEvent e) { }
    public virtual void OnFocusChanged(SDL.WindowEvent e) { }
    public virtual bool OnClickDown(SDL.MouseButtonEvent e) => false;
    public virtual bool OnClickUp(SDL.MouseButtonEvent e) => false;
    public virtual bool OnMouseWheel(SDL.MouseWheelEvent e) => false;
    public virtual void OnMouseMove(SDL.MouseMotionEvent e) { }
    public virtual bool OnKeyDown(SDL.KeyboardEvent e) => false;
    public virtual bool OnKeyUp(SDL.KeyboardEvent e) => false;
    public virtual bool OnKeyPress(SDL.TextInputEvent e) => false;
}