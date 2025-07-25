namespace StorybrewEditor.UserInterface;

using System;
using System.Globalization;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class Vector2Picker : Widget, Field
{
    readonly LinearLayout layout;
    readonly Textbox xTextbox, yTextbox;

    CommandPosition value;

    public Vector2Picker(WidgetManager manager) : base(manager)
    {
        Add(layout = new LinearLayout(manager)
        {
            FitChildren = true,
            Children =
            [
                new LinearLayout(manager)
                {
                    Horizontal = true,
                    FitChildren = true,
                    Fill = true,
                    Children =
                    [
                        new Label(Manager) { StyleName = "small", Text = "X", CanGrow = false },
                        xTextbox = new(manager) { EnterCommits = true }
                    ]
                },
                new LinearLayout(manager)
                {
                    Horizontal = true,
                    FitChildren = true,
                    Fill = true,
                    Children =
                    [
                        new Label(Manager) { StyleName = "small", Text = "Y", CanGrow = false },
                        yTextbox = new(manager) { EnterCommits = true }
                    ]
                }
            ]
        });

        updateWidgets();

        xTextbox.OnValueCommited += xTextbox_OnValueCommited;
        yTextbox.OnValueCommited += yTextbox_OnValueCommited;
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => Vector2.Zero;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public CommandPosition Value
    {
        get => value;
        set
        {
            if (this.value == value) return;

            this.value = value;

            updateWidgets();
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object FieldValue { get => Value; set => Value = (CommandPosition)value; }

    public event EventHandler OnValueChanged, OnValueCommited;

    void xTextbox_OnValueCommited(object sender, EventArgs e)
    {
        if (!double.TryParse(xTextbox.Value, CultureInfo.InvariantCulture, out var x))
        {
            updateWidgets();
            return;
        }

        value = new(x, value.Y);
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void yTextbox_OnValueCommited(object sender, EventArgs e)
    {
        if (!double.TryParse(yTextbox.Value, CultureInfo.InvariantCulture, out var y))
        {
            updateWidgets();
            return;
        }

        value = new(value.X, y);
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void updateWidgets()
    {
        using (var x = value.X.ToCharArray(provider: CultureInfo.InvariantCulture))
            xTextbox.SetValueSilent(x.AsReadOnlySpan());

        using (var y = value.Y.ToCharArray(provider: CultureInfo.InvariantCulture))
            yTextbox.SetValueSilent(y.AsReadOnlySpan());
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }
}