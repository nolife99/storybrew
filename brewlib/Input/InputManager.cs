namespace BrewLib.Input;

using System.Numerics;
using SDL3;

public sealed class InputManager
{
    readonly IInputHandler handler;
    public readonly nint Window;

    bool hasMouseHover;

    public InputManager(nint window, IInputHandler handler)
    {
        Window = window;
        this.handler = handler;
    }

    public bool HasMouseFocus => (SDL.GetWindowFlags(Window) & SDL.WindowFlags.Hidden) == 0 && hasMouseHover;

    public Vector2 MousePosition { get; private set; }

    public bool Control { get; private set; }
    public bool Shift { get; private set; }
    public bool Alt { get; private set; }

    public bool ControlOnly => Control && !Shift && !Alt;
    public bool ShiftOnly => !Control && Shift && !Alt;
    public bool AltOnly => !Control && !Shift && Alt;

    public bool ControlShiftOnly => Control && Shift && !Alt;
    public bool ControlAltOnly => Control && !Shift && Alt;
    public bool ShiftAltOnly => !Control && Shift && Alt;

    public void PumpEvents()
    {
        while (SDL.PollEvent(out var e))
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.WindowMouseEnter: window_MouseEnter(); break;
                case SDL.EventType.WindowMouseLeave: window_MouseLeave(); break;

                case SDL.EventType.WindowFocusGained:
                case SDL.EventType.WindowFocusLost:
                    window_FocusedChanged();
                    break;

                case SDL.EventType.KeyDown: window_KeyDown(e.Key); break;
                case SDL.EventType.KeyUp: window_KeyUp(e.Key); break;
                case SDL.EventType.TextInput: window_KeyPress(e.Text); break;
                case SDL.EventType.MouseMotion: window_MouseMove(e.Motion); break;
                case SDL.EventType.MouseButtonDown: window_MouseDown(e.Button); break;
                case SDL.EventType.MouseButtonUp: window_MouseUp(e.Button); break;
                case SDL.EventType.MouseWheel: window_MouseWheel(e.Wheel); break;

                case SDL.EventType.WindowResized: window_Resize(e.Window); break;
                case SDL.EventType.Quit: window_Close(e.Quit); break;
            }
    }

    public static void SetCursor(bool flag, SDL.SystemCursor cursor)
    {
        if (flag && SDL.GetCursor() == SDL.GetDefaultCursor() && cursor is not SDL.SystemCursor.Default)
            SDL.SetCursor(SDL.CreateSystemCursor(cursor));
        else if (SDL.GetCursor() != SDL.GetDefaultCursor())
        {
            SDL.DestroyCursor(SDL.GetCursor());
            SDL.SetCursor(SDL.GetDefaultCursor());
        }
    }

    void updateMouseFocus() => handler.OnFocusChanged(new() { Data1 = HasMouseFocus ? 1 : 0 });

    void window_MouseEnter()
    {
        hasMouseHover = true;
        updateMouseFocus();
    }

    void window_MouseLeave()
    {
        hasMouseHover = false;
        updateMouseFocus();
    }

    void window_FocusedChanged() => updateMouseFocus();

    void window_MouseDown(SDL.MouseButtonEvent e) => handler.OnClickDown(e);
    void window_MouseUp(SDL.MouseButtonEvent e) => handler.OnClickUp(e);

    void window_MouseMove(SDL.MouseMotionEvent e)
    {
        MousePosition = new(e.X, e.Y);

        handler.OnMouseMove(e);
    }

    void updateModifierState(SDL.KeyboardEvent e)
    {
        Control = (e.Mod & SDL.Keymod.Ctrl) != 0;
        Shift = (e.Mod & SDL.Keymod.Shift) != 0;
        Alt = (e.Mod & SDL.Keymod.Alt) != 0;
    }

    void window_KeyDown(SDL.KeyboardEvent e)
    {
        updateModifierState(e);
        handler.OnKeyDown(e);
    }

    void window_KeyUp(SDL.KeyboardEvent e)
    {
        updateModifierState(e);
        handler.OnKeyUp(e);
    }

    void window_KeyPress(SDL.TextInputEvent e) => handler.OnKeyPress(e);

    void window_MouseWheel(SDL.MouseWheelEvent e) => handler.OnMouseWheel(e);

    void window_Resize(SDL.WindowEvent e) => handler.OnResize(e);

    void window_Close(SDL.QuitEvent e) => handler.OnClose(e);
}