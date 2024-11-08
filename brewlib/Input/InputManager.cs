namespace BrewLib.Input;

using System;
using osuTK;
using osuTK.Input;
using Vector2 = System.Numerics.Vector2;

public sealed class InputManager : IDisposable
{
    readonly InputHandler handler;
    readonly GameWindow window;

    bool hasMouseHover;

    public InputManager(GameWindow window, InputHandler handler)
    {
        this.window = window;
        this.handler = handler;

        window.FocusedChanged += window_FocusedChanged;
        window.MouseEnter += window_MouseEnter;
        window.MouseLeave += window_MouseLeave;

        window.MouseUp += window_MouseUp;
        window.MouseDown += window_MouseDown;
        window.MouseWheel += window_MouseWheel;
        window.MouseMove += window_MouseMove;
        window.KeyDown += window_KeyDown;
        window.KeyUp += window_KeyUp;
        window.KeyPress += window_KeyPress;
    }

    public bool HasMouseFocus => window.Focused && hasMouseHover;

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

    public void Dispose()
    {
        window.FocusedChanged -= window_FocusedChanged;
        window.MouseEnter -= window_MouseEnter;
        window.MouseLeave -= window_MouseLeave;

        window.MouseUp -= window_MouseUp;
        window.MouseDown -= window_MouseDown;
        window.MouseWheel -= window_MouseWheel;
        window.MouseMove += window_MouseMove;
        window.KeyDown -= window_KeyDown;
        window.KeyUp -= window_KeyUp;
        window.KeyPress -= window_KeyPress;
    }

    void updateMouseFocus() => handler.OnFocusChanged(new FocusChangedEventArgs(HasMouseFocus));

    void window_MouseEnter(object sender, EventArgs e)
    {
        hasMouseHover = true;
        updateMouseFocus();
    }

    void window_MouseLeave(object sender, EventArgs e)
    {
        // https://github.com/osuTK/osuTK/issues/301

        // hasMouseHover = false;
        // updateMouseFocus();
    }

    void window_FocusedChanged(object sender, EventArgs e) => updateMouseFocus();

    void window_MouseDown(object sender, MouseButtonEventArgs e) => handler.OnClickDown(e);
    void window_MouseUp(object sender, MouseButtonEventArgs e) => handler.OnClickUp(e);

    void window_MouseMove(object sender, MouseMoveEventArgs e)
    {
        MousePosition = new Vector2(e.X, e.Y);
        handler.OnMouseMove(e);
    }

    void updateModifierState(KeyboardKeyEventArgs e)
    {
        Control = e.Modifiers.HasFlag(KeyModifiers.Control);
        Shift = e.Modifiers.HasFlag(KeyModifiers.Shift);
        Alt = e.Modifiers.HasFlag(KeyModifiers.Alt);
    }

    void window_KeyDown(object sender, KeyboardKeyEventArgs e)
    {
        updateModifierState(e);
        handler.OnKeyDown(e);
    }

    void window_KeyUp(object sender, KeyboardKeyEventArgs e)
    {
        updateModifierState(e);
        handler.OnKeyUp(e);
    }

    void window_KeyPress(object sender, KeyPressEventArgs e) => handler.OnKeyPress(e);
    void window_MouseWheel(object sender, MouseWheelEventArgs e) => handler.OnMouseWheel(e);
}

public class FocusChangedEventArgs(bool hasFocus) : EventArgs
{
    public bool HasFocus => hasFocus;
}