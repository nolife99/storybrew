namespace StorybrewEditor.UserInterface;

using System;
using System.Globalization;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public class Vector3Picker : Widget, Field
{
    readonly LinearLayout layout;
    readonly Textbox xTextbox, yTextbox, zTextbox;

    ValueArray<float> value;

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

    public ReadOnlySpan<float> Value
    {
        get => value.AsReadOnlySpan();
        set
        {
            if (this.value.AsReadOnlySpan().SequenceEqual(value)) return;

            this.value.Dispose();
            this.value = ValueArray.Create(value);

            updateWidgets();
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object FieldValue { get => Value.ToArray(); set => Value = (float[])value; }

    public event EventHandler OnValueChanged, OnValueCommited;

    void xTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var xCommit = xTextbox.Value;
        if (!float.TryParse(xCommit, CultureInfo.InvariantCulture, out var x))
        {
            updateWidgets();
            return;
        }

        using var temp = TempArray.Create([x, value[1], value[2]]);
        Value = temp.AsReadOnlySpan();

        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void yTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var yCommit = yTextbox.Value;
        if (!float.TryParse(yCommit, CultureInfo.InvariantCulture, out var y))
        {
            updateWidgets();
            return;
        }

        using var temp = TempArray.Create([value[0], y, value[2]]);
        Value = temp.AsReadOnlySpan();

        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void zTextbox_OnValueCommited(object sender, EventArgs e)
    {
        var zCommit = zTextbox.Value;
        if (!float.TryParse(zCommit, CultureInfo.InvariantCulture, out var z))
        {
            updateWidgets();
            return;
        }

        using var temp = TempArray.Create([value[0], value[1], z]);
        Value = temp.AsReadOnlySpan();

        OnValueCommited?.Invoke(this, EventArgs.Empty);
    }

    void updateWidgets()
    {
        using (var x = value[0].ToCharArray(provider: CultureInfo.InvariantCulture))
            xTextbox.SetValueSilent(x.AsReadOnlySpan());

        using (var y = value[1].ToCharArray(provider: CultureInfo.InvariantCulture))
            yTextbox.SetValueSilent(y.AsReadOnlySpan());

        using (var z = value[2].ToCharArray(provider: CultureInfo.InvariantCulture))
            zTextbox.SetValueSilent(z.AsReadOnlySpan());
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) value.Dispose();
        base.Dispose(disposing);
    }
}