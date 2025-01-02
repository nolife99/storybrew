namespace StorybrewEditor.UserInterface;

using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using BrewLib.UserInterface;

public class Vector3Picker : Widget, Field
{
    readonly LinearLayout layout;
    readonly Textbox xTextbox, yTextbox, zTextbox;

    float[] value;

    public Vector3Picker(WidgetManager manager) : base(manager)
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
                },
                new LinearLayout(manager)
                {
                    Horizontal = true,
                    FitChildren = true,
                    Fill = true,
                    Children =
                    [
                        new Label(Manager) { StyleName = "small", Text = "Z", CanGrow = false },
                        zTextbox = new Textbox(manager) { EnterCommits = true }
                    ]
                }
            ]
        });

        updateWidgets();

        xTextbox.OnValueCommited += xTextbox_OnValueCommited;
        yTextbox.OnValueCommited += yTextbox_OnValueCommited;
        zTextbox.OnValueCommited += zTextbox_OnValueCommited;
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => Vector2.Zero;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public float[] Value
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

    public object FieldValue { get => Value; set => Value = Unsafe.As<float[]>(value); }

    public event EventHandler OnValueChanged, OnValueCommited;

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

        Value = [x, value[1], value[2]];
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

        Value = [value[0], y, value[2]];
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void zTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var zCommit = zTextbox.Value;

        float z;
        try
        {
            z = float.Parse(zCommit, CultureInfo.InvariantCulture);
        }
        catch
        {
            updateWidgets();
            return;
        }

        value = [value[0], value[1], z];
        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void updateWidgets()
    {
        xTextbox.SetValueSilent(value[0].ToString(CultureInfo.InvariantCulture));
        yTextbox.SetValueSilent(value[1].ToString(CultureInfo.InvariantCulture));
        zTextbox.SetValueSilent(value[2].ToString(CultureInfo.InvariantCulture));
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }
}