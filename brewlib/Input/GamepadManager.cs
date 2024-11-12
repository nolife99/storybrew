namespace BrewLib.Input;

using System;

public class GamepadEventArgs : EventArgs;

public class GamepadButtonEventArgs(GamepadButton button) : GamepadEventArgs
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