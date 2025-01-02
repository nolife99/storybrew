namespace StorybrewEditor.UserInterface;

using System;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning.Styles;
using ScreenLayers;
using Skinning.Styles;
using StorybrewCommon.Util;

public class Selectbox : Widget, Field
{
    readonly Button button;

    NamedValue[] options;

    object value;

    public Selectbox(WidgetManager manager) : base(manager)
    {
        Add(button = new(manager));
        button.OnClick += (_, _) =>
        {
            if (options is null) return;

            if (options.Length > 2)
                Manager.ScreenLayerManager.ShowContextMenu("Select a value",
                    optionValue => Value = optionValue.Value,
                    options);
            else
            {
                var optionFound = false;
                foreach (var option in options)
                    if (optionFound)
                    {
                        Value = option.Value;
                        optionFound = false;
                        break;
                    }
                    else if (option.Value.Equals(value)) optionFound = true;

                if (optionFound) Value = options[0].Value;
            }
        };
    }

    public override Vector2 MinSize => button.MinSize;
    public override Vector2 MaxSize => button.MaxSize;
    public override Vector2 PreferredSize => button.PreferredSize;

    public NamedValue[] Options
    {
        get => options;
        set
        {
            if (options == value) return;

            options = value;

            button.Text = findValueName(this.value);
        }
    }

    public object Value
    {
        get => value;
        set
        {
            if (this.value == value) return;

            this.value = value;

            button.Text = findValueName(this.value);
            OnValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<SelectboxStyle>(BuildStyleName());

    public object FieldValue { get => Value; set => Value = value; }

    public event EventHandler OnValueChanged;

    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var selectboxStyle = (SelectboxStyle)style;

        button.StyleName = selectboxStyle.ButtonStyle;
    }

    protected override void Layout()
    {
        base.Layout();
        button.Size = Size;
    }

    string findValueName(object value)
    {
        if (options is null) return "";

        foreach (var option in options)
            if (option.Value.Equals(value))
                return option.Name;

        return "";
    }
}