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
    bool OnKeyDown(KeyboardEvent e);
    bool OnKeyUp(KeyboardEvent e);
    bool OnKeyPress(TextInputEvent e);
}