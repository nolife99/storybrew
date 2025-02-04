﻿namespace BrewLib.Input;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

public sealed class InputManager : IDisposable
{
    readonly IInputHandler handler;
    internal readonly NativeWindow window;

    bool hasMouseHover;

    public InputManager(NativeWindow window, IInputHandler handler)
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
        window.TextInput += window_KeyPress;
    }

    public bool HasMouseFocus => window.IsFocused && hasMouseHover;

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
        window.TextInput -= window_KeyPress;
    }

    void updateMouseFocus() => handler.OnFocusChanged(new FocusedChangedEventArgs(HasMouseFocus));

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

    void window_FocusedChanged(FocusedChangedEventArgs e) => updateMouseFocus();

    void window_MouseDown(MouseButtonEventArgs e) => handler.OnClickDown(e);
    void window_MouseUp(MouseButtonEventArgs e) => handler.OnClickUp(e);

    void window_MouseMove(MouseMoveEventArgs e)
    {
        var pos = e.Position;
        MousePosition = Unsafe.ReadUnaligned<Vector2>(ref Unsafe.As<float, byte>(ref pos.X));

        handler.OnMouseMove(e);
    }

    void updateModifierState(KeyboardKeyEventArgs e)
    {
        Control = (e.Modifiers & KeyModifiers.Control) != 0;
        Shift = (e.Modifiers & KeyModifiers.Shift) != 0;
        Alt = (e.Modifiers & KeyModifiers.Alt) != 0;
    }

    void window_KeyDown(KeyboardKeyEventArgs e)
    {
        updateModifierState(e);
        handler.OnKeyDown(e);
    }

    void window_KeyUp(KeyboardKeyEventArgs e)
    {
        updateModifierState(e);
        handler.OnKeyUp(e);
    }

    void window_KeyPress(TextInputEventArgs e) => handler.OnKeyPress(e);

    void window_MouseWheel(MouseWheelEventArgs e) => handler.OnMouseWheel(e);
}