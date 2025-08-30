namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.Input;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using SDL3;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class Textbox : Widget, Field
{
    readonly bool acceptMultiline;
    readonly Sprite cursorLine;
    readonly Label label, content;

    int cursorPosition, selectionStart;

    bool hasFocus, hovered, hasCommitPending;

    public Textbox(WidgetManager manager) : base(manager)
    {
        DefaultSize = new(200, 0);

        cursorLine = new() { Texture = DrawState.WhitePixel, ScaleMode = ScaleMode.Fill, Color = Color.White };

        Add(content = new(manager) { AnchorFrom = BoxAlignment.BottomLeft, AnchorTo = BoxAlignment.BottomLeft });

        Add(label = new(manager) { AnchorFrom = BoxAlignment.TopLeft, AnchorTo = BoxAlignment.TopLeft });

        OnFocusChange += (_, e) =>
        {
            if (hasFocus == e.HasFocus) return;

            if (hasFocus && hasCommitPending)
            {
                OnValueCommited?.Invoke(this, EventArgs.Empty);
                hasCommitPending = false;
            }

            hasFocus = e.HasFocus;
            RefreshStyle();
        };

        OnHovered += (_, e) =>
        {
            hovered = e.Hovered;
            InputManager.SetCursor(hovered, SDL.SystemCursor.Text);

            RefreshStyle();
        };

        OnKeyDown += (_, e) =>
        {
            if (!hasFocus) return false;

            var inputManager = manager.InputManager;
            switch (e.Key)
            {
                case SDL.Keycode.Escape:
                    if (hasFocus) manager.KeyboardFocus = null;
                    break;

                case SDL.Keycode.Backspace:
                    if (selectionStart > 0 && selectionStart == cursorPosition) --selectionStart;

                    ReplaceSelection("");
                    break;

                case SDL.Keycode.Delete:
                    if (selectionStart < Value.Length && selectionStart == cursorPosition) ++cursorPosition;

                    ReplaceSelection("");
                    break;

                case SDL.Keycode.A:
                    if (inputManager.ControlOnly) SelectAll();
                    break;

                case SDL.Keycode.C:
                    if (inputManager.ControlOnly)
                        ClipboardHelper.SetText(selectionStart != cursorPosition ?
                            Value.Slice(SelectionLeft, SelectionLength) :
                            Value);

                    break;

                case SDL.Keycode.V:
                    if (inputManager.ControlOnly)
                    {
                        var clipboardText = ClipboardHelper.GetText();
                        if (clipboardText is not null)
                        {
                            if (!AcceptMultiline) clipboardText = clipboardText.Replace("\n", "");

                            ReplaceSelection(clipboardText);
                        }
                    }

                    break;

                case SDL.Keycode.X:
                    if (inputManager.ControlOnly)
                    {
                        if (selectionStart == cursorPosition) SelectAll();
                        ClipboardHelper.SetText(Value.Slice(SelectionLeft, SelectionLength));

                        ReplaceSelection("");
                    }

                    break;

                case SDL.Keycode.Left:
                    if (inputManager.Shift)
                    {
                        if (cursorPosition > 0) --cursorPosition;
                    }
                    else if (selectionStart != cursorPosition) SelectionRight = SelectionLeft;
                    else if (cursorPosition > 0) cursorPosition = --selectionStart;

                    break;

                case SDL.Keycode.Right:
                    if (inputManager.Shift)
                    {
                        if (cursorPosition < Value.Length) cursorPosition++;
                    }
                    else if (selectionStart != cursorPosition) SelectionLeft = SelectionRight;
                    else if (cursorPosition < Value.Length) selectionStart = ++cursorPosition;

                    break;

                case SDL.Keycode.Up:
                    cursorPosition = content.GetCharacterIndexAbove(cursorPosition);

                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case SDL.Keycode.Down:
                    cursorPosition = content.GetCharacterIndexBelow(cursorPosition);

                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case SDL.Keycode.Home:
                    cursorPosition = 0;
                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case SDL.Keycode.End:
                    cursorPosition = Value.Length;
                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case SDL.Keycode.Return:
                    if (AcceptMultiline && (!EnterCommits || inputManager.Shift)) ReplaceSelection("\n");
                    else if (EnterCommits && hasCommitPending)
                    {
                        OnValueCommited?.Invoke(this, EventArgs.Empty);
                        hasCommitPending = false;
                    }

                    break;
            }

            return true;
        };

        OnKeyUp += (_, _) => hasFocus;
        OnKeyPress += (_, e) =>
        {
            if (!hasFocus) return false;

            ReplaceSelection(e.SDLUtf8ToString(stackalloc char[e.SDLUtf8ToStringLength()]));
            return true;
        };

        OnClickDown += (_, _) =>
        {
            manager.KeyboardFocus = this;
            var fromScreen = Manager.Camera.FromScreen(manager.InputManager.MousePosition);

            selectionStart = cursorPosition = content.GetCharacterIndexAt(new(fromScreen.X, fromScreen.Y));

            return true;
        };

        OnClickMove += (_, e) =>
        {
            var fromScreen = Manager.Camera.FromScreen(new Vector2(e.X, e.Y));
            cursorPosition = content.GetCharacterIndexAt(new(fromScreen.X, fromScreen.Y));
        };
    }

    public bool EnterCommits { get; init; } = true;

    int SelectionLeft
    {
        get => int.Min(selectionStart, cursorPosition);
        set
        {
            if (selectionStart < cursorPosition) selectionStart = value;
            else cursorPosition = value;
        }
    }

    int SelectionRight
    {
        get => int.Max(selectionStart, cursorPosition);
        set
        {
            if (selectionStart > cursorPosition) selectionStart = value;
            else cursorPosition = value;
        }
    }

    int SelectionLength => int.Abs(cursorPosition - selectionStart);

    public override Vector2 MinSize => new(0, PreferredSize.Y);

    public override Vector2 MaxSize => new(0, PreferredSize.Y);

    public override Vector2 PreferredSize
    {
        get
        {
            var contentSize = content.PreferredSize;
            if (label.Text.IsWhiteSpace()) return new(float.Max(contentSize.X, DefaultSize.X), contentSize.Y);

            var labelSize = label.PreferredSize;
            return new(float.Max(labelSize.X, DefaultSize.X), labelSize.Y + contentSize.Y);
        }
    }

    public ReadOnlySpan<char> LabelText { get => label.Text; set => label.Text = value; }

    public ReadOnlySpan<char> Value
    {
        get => content.Text;
        set
        {
            if (content.Text.SequenceEqual(value)) return;

            SetValueSilent(value);

            if (hasFocus) hasCommitPending = true;
            OnValueChanged?.Invoke(this, EventArgs.Empty);
            if (!hasFocus) OnValueCommited?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool AcceptMultiline
    {
        get => acceptMultiline;
        init
        {
            if (acceptMultiline == value) return;

            acceptMultiline = value;

            if (acceptMultiline) return;

            using var temp = TempList.Create(Value);
            temp.RemoveAll(c => c == '\n');

            Value = temp.AsReadOnlySpan();
        }
    }

    protected override WidgetStyle Style
        => Manager.Skin.GetStyle<TextboxStyle>(BuildStyleName(hovered ? "hover" : null, hasFocus ? "focus" : null));

    public object FieldValue { get => Value.ToString(); set => Value = (string)value; }

    public event EventHandler OnValueChanged, OnValueCommited;

    public void SetValueSilent(ReadOnlySpan<char> value)
    {
        content.Text = value;
        if (selectionStart > content.Text.Length) selectionStart = content.Text.Length;

        if (cursorPosition > content.Text.Length) cursorPosition = content.Text.Length;
    }

    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var textboxStyle = (TextboxStyle)style;

        label.StyleName = textboxStyle.LabelStyle;
        content.StyleName = textboxStyle.ContentStyle;
    }

    protected override void DrawForeground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawForeground(drawContext, actualOpacity);
        if (!hasFocus) return;

        if (cursorPosition != selectionStart)
            content.ForTextBounds(SelectionLeft,
                SelectionRight,
                (selectionBounds, state) => state.Item3.cursorLine.Draw(state.drawContext,
                    state.Item3.Manager.Camera,
                    selectionBounds,
                    state.actualOpacity * .2f),
                (drawContext, actualOpacity, this));

        var bounds = content.GetCharacterBounds(cursorPosition);
        Vector2 position = new(bounds.X, bounds.Y + bounds.Height * .15f),
            scale = new(Manager.PixelSize, bounds.Height * .8f);

        cursorLine.Draw(drawContext, Manager.Camera, new(position.X, position.Y, scale.X, scale.Y), actualOpacity);
    }

    protected override void Layout()
    {
        base.Layout();
        content.Size = new(Size.X, content.PreferredSize.Y);
        label.Size = new(Size.X, label.PreferredSize.Y);
    }

    public void SelectAll()
    {
        selectionStart = 0;
        cursorPosition = Value.Length;
    }

    void ReplaceSelection(scoped ReadOnlySpan<char> text)
    {
        var left = SelectionLeft;
        var right = SelectionRight;

        using var newValue = TempList.Create(Value);
        if (left != right) newValue.RemoveRange(left, right - left);
        newValue.InsertRange(left, text);

        Value = newValue.AsReadOnlySpan();

        cursorPosition = selectionStart = SelectionLeft + text.Length;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cursorLine.Dispose();
            InputManager.SetCursor(hovered, SDL.SystemCursor.Default);
        }

        base.Dispose(disposing);
    }
}