using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

namespace BrewLib.Input;

using System;
using System.Numerics;
using SDL3;

public sealed class InputManager(nint window, IInputHandler handler)
{
    public readonly IInputHandler Handler = handler;
    public readonly nint Window = window;

    bool hasMouseHover;

    public bool HasMouseFocus => (SDL.GetWindowFlags(Window) & WindowFlags.Hidden) == 0 && hasMouseHover;

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

    public void Update()
    {
        SDL.PumpEvents();
        using var events = TempArray.Create<Event>(SDL.PeepEvents(0,
            0,
            EventAction.PeekEvent,
            EventType.First,
            EventType.Last));

        if (SDL.PeepEvents(events.AsSpan(), EventAction.GetEvent, EventType.First, EventType.Last) == -1)
            throw new InvalidOperationException($"Unable to get events: {SDL.GetError()}");

        foreach (ref var e in events)
            switch (e.Type)
            {
                case EventType.WindowMouseEnter: window_MouseEnter(); break;
                case EventType.WindowMouseLeave: window_MouseLeave(); break;

                case EventType.WindowFocusGained:
                case EventType.WindowFocusLost:
                    window_FocusedChanged();
                    break;

                case EventType.KeyDown: window_KeyDown(e.Key); break;
                case EventType.KeyUp: window_KeyUp(e.Key); break;
                case EventType.TextInput: window_KeyPress(e.Text); break;
                case EventType.MouseMotion: window_MouseMove(e.Motion); break;
                case EventType.MouseButtonDown: window_MouseDown(e.Button); break;
                case EventType.MouseButtonUp: window_MouseUp(e.Button); break;
                case EventType.MouseWheel: window_MouseWheel(e.Wheel); break;

                case EventType.WindowResized:
                case EventType.WindowPixelSizeChanged:
                    window_Resize(e.Window);
                    break;
                case EventType.Quit: window_Close(e.Quit); break;
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

    void updateMouseFocus() => Handler.OnFocusChanged(new() { Data1 = HasMouseFocus ? 1 : 0 });

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

    void window_MouseDown(SDL.MouseButtonEvent e) => Handler.OnClickDown(e);
    void window_MouseUp(SDL.MouseButtonEvent e) => Handler.OnClickUp(e);

    void window_MouseMove(SDL.MouseMotionEvent e)
    {
        MousePosition = new(e.X, e.Y);

        Handler.OnMouseMove(e);
    }

    void updateModifierState(KeyboardEvent e)
    {
        Control = (e.Mod & SDL.Keymod.Ctrl) != 0;
        Shift = (e.Mod & SDL.Keymod.Shift) != 0;
        Alt = (e.Mod & SDL.Keymod.Alt) != 0;
    }

    void window_KeyDown(KeyboardEvent e)
    {
        updateModifierState(e);
        Handler.OnKeyDown(e);
    }

    void window_KeyUp(KeyboardEvent e)
    {
        updateModifierState(e);
        Handler.OnKeyUp(e);
    }

    void window_KeyPress(TextInputEvent e) => Handler.OnKeyPress(e);

    void window_MouseWheel(SDL.MouseWheelEvent e) => Handler.OnMouseWheel(e);

    void window_Resize(WindowEvent e)
    {
        SDL.GetWindowSizeInPixels(Window, out var width, out var height);
        Handler.OnResize(new() { Data1 = width, Data2 = height });
    }

    void window_Close(SDL.QuitEvent e) => Handler.OnClose(e);
}
