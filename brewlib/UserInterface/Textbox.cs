namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using Graphics;
using Graphics.Drawables;
using SixLabors.ImageSharp;
using Skinning.Styles;
using Util;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class Textbox : Widget, Field
{
    readonly Label label, content;
    bool acceptMultiline, hasFocus, hovered, hasCommitPending;
    Sprite cursorLine;

    int cursorPosition, selectionStart;
    public bool EnterCommits = true;

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
            RefreshStyle();
        };

        OnKeyDown += (_, e) =>
        {
            if (!hasFocus) return false;

            var inputManager = manager.InputManager;
            switch (e.Key)
            {
                case Keys.Escape:
                    if (hasFocus) manager.KeyboardFocus = null;
                    break;

                case Keys.Backspace:
                    if (selectionStart > 0 && selectionStart == cursorPosition) selectionStart--;
                    ReplaceSelection("");
                    break;

                case Keys.Delete:
                    if (selectionStart < Value.Length && selectionStart == cursorPosition) cursorPosition++;
                    ReplaceSelection("");
                    break;

                case Keys.A:
                    if (inputManager.ControlOnly) SelectAll();
                    break;

                case Keys.C:
                    if (inputManager.ControlOnly)
                        ClipboardHelper.SetText(selectionStart != cursorPosition ?
                            Value.AsSpan().Slice(SelectionLeft, SelectionLength) :
                            Value);

                    break;

                case Keys.V:
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

                case Keys.X:
                    if (inputManager.ControlOnly)
                    {
                        if (selectionStart == cursorPosition) SelectAll();
                        ClipboardHelper.SetText(Value.AsSpan().Slice(SelectionLeft, SelectionLength));
                        ReplaceSelection("");
                    }

                    break;

                case Keys.Left:
                    if (inputManager.Shift)
                    {
                        if (cursorPosition > 0) cursorPosition--;
                    }
                    else if (selectionStart != cursorPosition) SelectionRight = SelectionLeft;
                    else if (cursorPosition > 0) cursorPosition = --selectionStart;

                    break;

                case Keys.Right:
                    if (inputManager.Shift)
                    {
                        if (cursorPosition < Value.Length) cursorPosition++;
                    }
                    else if (selectionStart != cursorPosition) SelectionLeft = SelectionRight;
                    else if (cursorPosition < Value.Length) selectionStart = ++cursorPosition;

                    break;

                case Keys.Up:
                    cursorPosition = content.GetCharacterIndexAbove(cursorPosition);
                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case Keys.Down:
                    cursorPosition = content.GetCharacterIndexBelow(cursorPosition);
                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case Keys.Home:
                    cursorPosition = 0;
                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case Keys.End:
                    cursorPosition = Value.Length;
                    if (!inputManager.Shift) selectionStart = cursorPosition;
                    break;

                case Keys.Enter:
                case Keys.KeyPadEnter:
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
            ReplaceSelection(e.AsString);
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

    int SelectionLeft
    {
        get => Math.Min(selectionStart, cursorPosition);
        set
        {
            if (selectionStart < cursorPosition) selectionStart = value;
            else cursorPosition = value;
        }
    }

    int SelectionRight
    {
        get => Math.Max(selectionStart, cursorPosition);
        set
        {
            if (selectionStart > cursorPosition) selectionStart = value;
            else cursorPosition = value;
        }
    }

    int SelectionLength => Math.Abs(cursorPosition - selectionStart);

    public override Vector2 MinSize => PreferredSize with { X = 0 };

    public override Vector2 MaxSize => PreferredSize with { X = 0 };

    public override Vector2 PreferredSize
    {
        get
        {
            var contentSize = content.PreferredSize;
            if (string.IsNullOrWhiteSpace(label.Text)) return contentSize with { X = Math.Max(contentSize.X, DefaultSize.X) };

            var labelSize = label.PreferredSize;
            return new(Math.Max(labelSize.X, DefaultSize.X), labelSize.Y + contentSize.Y);
        }
    }

    public string LabelText { get => label.Text; set => label.Text = value; }

    public string Value
    {
        get => content.Text;
        set
        {
            if (content.Text == value) return;
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

            if (!acceptMultiline) Value = Value.Replace("\n", "");
        }
    }

    protected override WidgetStyle Style
        => Manager.Skin.GetStyle<TextboxStyle>(BuildStyleName(hovered ? "hover" : null, hasFocus ? "focus" : null));

    public object FieldValue { get => Value; set => Value = (string)value; }

    public event EventHandler OnValueChanged, OnValueCommited;

    public void SetValueSilent(string value)
    {
        content.Text = value ?? "";
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
            content.ForTextBounds(SelectionLeft, SelectionRight,
                selectionBounds => cursorLine.Draw(drawContext, Manager.Camera, selectionBounds, actualOpacity * .2f));

        var bounds = content.GetCharacterBounds(cursorPosition);
        Vector2 position = new(bounds.Left, bounds.Top + bounds.Height * .2f),
            scale = new(Manager.PixelSize, bounds.Height * .6f);

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
    void ReplaceSelection(string text)
    {
        var left = SelectionLeft;
        var right = SelectionRight;

        var newValue = Value;
        if (left != right) newValue = newValue.Remove(left, right - left);
        newValue = newValue.Insert(left, text);

        Value = newValue;
        cursorPosition = selectionStart = SelectionLeft + text.Length;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) cursorLine.Dispose();
        base.Dispose(disposing);
    }
}