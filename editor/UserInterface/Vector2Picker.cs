namespace StorybrewEditor.UserInterface;

using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.UserInterface;
using StorybrewCommon.Storyboarding.CommandValues;

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

    public object FieldValue { get => Value; set => Value = Unsafe.Unbox<CommandPosition>(value); }

    public event EventHandler OnValueChanged, OnValueCommited;

    void xTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var xCommit = xTextbox.Value;

        double x;
        try
        {
            x = double.Parse(xCommit, CultureInfo.InvariantCulture);
        }
        catch
        {
            updateWidgets();
            return;
        }

        value = new(x, value.Y);
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void yTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var yCommit = yTextbox.Value;

        double y;
        try
        {
            y = double.Parse(yCommit, CultureInfo.InvariantCulture);
        }
        catch
        {
            updateWidgets();
            return;
        }

        value = new(value.X, y);
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void updateWidgets()
    {
        xTextbox.SetValueSilent(value.X.ToString());
        yTextbox.SetValueSilent(value.Y.ToString());
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }
}