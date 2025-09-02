namespace BrewLib.Input;

using SDL3;

public interface IInputHandler
{
    void OnClose(SDL.QuitEvent e);
    void OnResize(WindowEvent e);
    void OnFocusChanged(WindowEvent e);
    bool OnClickDown(SDL.MouseButtonEvent e);
    bool OnClickUp(SDL.MouseButtonEvent e);
    bool OnMouseWheel(SDL.MouseWheelEvent e);
    void OnMouseMove(SDL.MouseMotionEvent e);
    bool OnKeyDown(SDL.KeyboardEvent e);
    bool OnKeyUp(SDL.KeyboardEvent e);
    bool OnKeyPress(SDL.TextInputEvent e);
}