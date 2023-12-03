using BrewLib.UserInterface;
using System.Numerics;
using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Globalization;

namespace StorybrewEditor.UserInterface;

public class Vector2Picker : Widget, Field
{
    readonly LinearLayout layout;
    readonly Textbox xTextbox, yTextbox;

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => Vector2.Zero;
    public override Vector2 PreferredSize => layout.PreferredSize;

    CommandPosition value;
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
    public object FieldValue
    {
        get => Value;
        set => Value = (CommandPosition)value;
    }

    public event EventHandler OnValueChanged, OnValueCommited;

    public Vector2Picker(WidgetManager manager) : base(manager)
    {
        Add(layout = new LinearLayout(manager)
        {
            FitChildren = true,
            Children = new Widget[]
            {
                new LinearLayout(manager)
                {
                    Horizontal = true,
                    FitChildren = true,
                    Fill = true,
                    Children = new Widget[]
                    {
                        new Label(Manager)
                        {
                            StyleName = "small",
                            Text = "X",
                            CanGrow = false
                        },
                        xTextbox = new(manager)
                        {
                            EnterCommits = true
                        }
                    }
                },
                new LinearLayout(manager)
                {
                    Horizontal = true,
                    FitChildren = true,
                    Fill = true,
                    Children = new Widget[]
                    {
                        new Label(Manager)
                        {
                            StyleName = "small",
                            Text = "Y",
                            CanGrow = false
                        },
                        yTextbox = new(manager)
                        {
                            EnterCommits = true
                        }
                    }
                }
            }
        });
        updateWidgets();

        xTextbox.OnValueCommited += xTextbox_OnValueCommited;
        yTextbox.OnValueCommited += yTextbox_OnValueCommited;
    }
    void xTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var xCommit = xTextbox.Value;

        float x;
        try
        {
            x = float.Parse(xCommit, CultureInfo.InvariantCulture);
        }
        catch
        {
            updateWidgets();
            return;
        }
        Value = new(x, value.Y);
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }
    void yTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var yCommit = yTextbox.Value;

        float y;
        try
        {
            y = float.Parse(yCommit, CultureInfo.InvariantCulture);
        }
        catch
        {
            updateWidgets();
            return;
        }
        Value = new(value.X, y);
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