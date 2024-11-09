namespace BrewLib.Input;

using System;
using osuTK;
using osuTK.Input;

public class GamepadManager(int gamepadIndex)
{
    KeyboardState? keyboardState;
    GamepadButton previousPressedButtons, pressedButtons;
    GamePadState state = GamePad.GetState(gamepadIndex);
    Vector2 thumb, thumbAlt;

    public float TriggerInnerDeadzone = .1f, TriggerOuterDeadzone = .1f, AxisInnerDeadzone = .3f, AxisOuterDeadzone = .1f;

    public int PlayerIndex => gamepadIndex;
    public bool Connected => state.IsConnected;

    public float TriggerLeft { get; private set; }
    public float TriggerRight { get; private set; }

    public Vector2 Thumb => thumb;
    public Vector2 ThumbAlt => thumbAlt;

    public bool IsDown(GamepadButton button) => (pressedButtons & button) != 0;

    public bool IsPressed(GamepadButton button) => (previousPressedButtons & button) == 0 && (pressedButtons & button) != 0;

    public bool IsReleased(GamepadButton button) => (previousPressedButtons & button) != 0 && (pressedButtons & button) == 0;

    public event EventHandler<GamepadEventArgs> OnConnected;
    public event EventHandler<GamepadButtonEventArgs> OnButtonDown, OnButtonUp;

    public void Update()
    {
        var wasConnected = state.IsConnected;
        previousPressedButtons = pressedButtons;

        state = GamePad.GetState(gamepadIndex);
        keyboardState = gamepadIndex == 0 && !state.IsConnected ? Keyboard.GetState() : null;
        pressedButtons = 0;

        var buttonsState = state.Buttons;
        var dPadState = state.DPad;

        thumb = applyAxisFilters(state.ThumbSticks.Left);
        thumbAlt = applyAxisFilters(state.ThumbSticks.Right);
        TriggerLeft = applyTriggerFilters(state.Triggers.Left);
        TriggerRight = applyTriggerFilters(state.Triggers.Right);

        updateButton(dPadState.Left, GamepadButton.DPadLeft, Key.F);
        updateButton(dPadState.Up, GamepadButton.DPadUp, Key.T);
        updateButton(dPadState.Right, GamepadButton.DPadRight, Key.H);
        updateButton(dPadState.Down, GamepadButton.DPadDown, Key.G);
        updateAxis(thumb.X, GamepadButton.ThumbLeft, GamepadButton.ThumbRight, Key.A, Key.D);
        updateAxis(thumb.Y, GamepadButton.ThumbDown, GamepadButton.ThumbUp, Key.S, Key.W);
        updateAxis(thumbAlt.X, GamepadButton.ThumbAltLeft, GamepadButton.ThumbAltRight, Key.Left, Key.Right);
        updateAxis(thumbAlt.Y, GamepadButton.ThumbAltDown, GamepadButton.ThumbAltUp, Key.Down, Key.Up);
        updateButton(buttonsState.A, GamepadButton.A, Key.J);
        updateButton(buttonsState.B, GamepadButton.B, Key.K);
        updateButton(buttonsState.X, GamepadButton.X, Key.U);
        updateButton(buttonsState.Y, GamepadButton.Y, Key.I);
        updateButton(buttonsState.LeftShoulder, GamepadButton.LeftShoulder, Key.O);
        updateButton(buttonsState.RightShoulder, GamepadButton.RightShoulder, Key.L);
        updateTrigger(TriggerLeft, GamepadButton.LeftTrigger, Key.P);
        updateTrigger(TriggerRight, GamepadButton.RightTrigger, Key.Semicolon);
        updateButton(buttonsState.LeftStick, GamepadButton.Thumb, Key.Q);
        updateButton(buttonsState.RightStick, GamepadButton.ThumbAlt, Key.E);
        updateButton(buttonsState.Start, GamepadButton.Start, Key.Enter);
        updateButton(buttonsState.Back, GamepadButton.Select, Key.Escape);
        updateButton(buttonsState.BigButton, GamepadButton.Home, Key.BackSpace);

        if (!state.IsConnected)
        {
            if (IsDown(GamepadButton.ThumbLeft)) thumb.X -= 1;
            if (IsDown(GamepadButton.ThumbRight)) thumb.X += 1;
            if (IsDown(GamepadButton.ThumbDown)) thumb.Y -= 1;
            if (IsDown(GamepadButton.ThumbUp)) thumb.Y += 1;

            var thumbLeftLength = thumb.Length;
            if (thumbLeftLength > 0) thumb /= thumbLeftLength;

            if (IsDown(GamepadButton.ThumbAltLeft)) thumbAlt.X -= 1;
            if (IsDown(GamepadButton.ThumbAltRight)) thumbAlt.X += 1;
            if (IsDown(GamepadButton.ThumbAltDown)) thumbAlt.Y -= 1;
            if (IsDown(GamepadButton.ThumbAltUp)) thumbAlt.Y += 1;

            var thumbRightLength = thumbAlt.Length;
            if (thumbRightLength > 0) thumbAlt /= thumbRightLength;

            TriggerLeft = IsDown(GamepadButton.LeftTrigger) ? 1 : 0;
            TriggerRight = IsDown(GamepadButton.RightTrigger) ? 1 : 0;
        }

        if (wasConnected != state.IsConnected) OnConnected?.Invoke(this, new GamepadEventArgs(this));

        var changedButtons = pressedButtons ^ previousPressedButtons;
        for (var button = 1; button <= (int)changedButtons; button <<= 1)
        {
            if (((int)changedButtons & button) == 0) continue;

            if (((int)pressedButtons & button) != 0)
                OnButtonDown?.Invoke(this, new GamepadButtonEventArgs(this, (GamepadButton)button));
            else OnButtonUp?.Invoke(this, new GamepadButtonEventArgs(this, (GamepadButton)button));
        }
    }

    void updateButton(ButtonState state, GamepadButton button, Key key)
    {
        if (state is ButtonState.Pressed || isKeyDown(key)) pressedButtons |= button;
    }
    void updateTrigger(float state, GamepadButton button, Key key)
    {
        if (state > .5f || isKeyDown(key)) pressedButtons |= button;
    }
    void updateAxis(float state, GamepadButton negativeButton, GamepadButton positiveButton, Key negativeKey, Key positiveKey)
    {
        if (state > .5f || isKeyDown(positiveKey)) pressedButtons |= positiveButton;
        if (state < -.5f || isKeyDown(negativeKey)) pressedButtons |= negativeButton;
    }

    float applyTriggerFilters(float value)
    {
        if (value < TriggerInnerDeadzone) return 0;
        return Math.Min(1, (value - TriggerInnerDeadzone) / (1 - TriggerOuterDeadzone - TriggerInnerDeadzone)) / value;
    }
    Vector2 applyAxisFilters(Vector2 value)
    {
        var length = value.Length;
        return length < AxisInnerDeadzone ?
            Vector2.Zero :
            value * Math.Min(1, (length - AxisInnerDeadzone) / (1 - AxisOuterDeadzone - AxisInnerDeadzone)) / length;
    }

    bool isKeyDown(Key key) => keyboardState?.IsKeyDown(key) ?? false;
}

public class GamepadEventArgs(GamepadManager manager) : EventArgs
{
    public GamepadManager Manager = manager;
}

public class GamepadButtonEventArgs(GamepadManager manager, GamepadButton button) : GamepadEventArgs(manager)
{
    public GamepadButton Button = button;
    public override string ToString() => Button.ToString();
}

[Flags] public enum GamepadButton
{
    None = 0, DPadLeft = 1 << 0, DPadUp = 1 << 1,
    DPadRight = 1 << 2, DPadDown = 1 << 3, ThumbLeft = 1 << 4,
    ThumbUp = 1 << 5, ThumbRight = 1 << 6, ThumbDown = 1 << 7,
    ThumbAltLeft = 1 << 8, ThumbAltUp = 1 << 9, ThumbAltRight = 1 << 10,
    ThumbAltDown = 1 << 11, A = 1 << 12, B = 1 << 13,
    X = 1 << 14, Y = 1 << 15, LeftShoulder = 1 << 16,
    RightShoulder = 1 << 17, LeftTrigger = 1 << 18, RightTrigger = 1 << 19,
    Thumb = 1 << 20, ThumbAlt = 1 << 21, Start = 1 << 22,
    Select = 1 << 23, Home = 1 << 24
}