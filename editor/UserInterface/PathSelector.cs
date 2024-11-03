using System;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using StorybrewEditor.ScreenLayers;
using StorybrewEditor.UserInterface.Skinning.Styles;

namespace StorybrewEditor.UserInterface;

public class PathSelector : Widget
{
    readonly LinearLayout layout;
    readonly Textbox textbox;
    readonly Button button;

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public string LabelText { get => textbox.LabelText; set => textbox.LabelText = value; }
    public string Value { get => textbox.Value; set => textbox.Value = value; }

    public string Filter = "All files (*.*)|*.*";
    public string SaveExtension = "";

    public event EventHandler OnValueChanged, OnValueCommited;

    public PathSelector(WidgetManager manager, PathSelectorMode mode) : base(manager)
    {
        Add(layout = new(manager)
        {
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Horizontal = true,
            Fill = true,
            FitChildren = true,
            Children =
            [
                textbox = new(manager)
                {
                    AnchorFrom = BoxAlignment.BottomLeft,
                    AnchorTo = BoxAlignment.BottomLeft
                },
                button = new(manager)
                {
                    Icon = IconFont.FolderOpen,
                    Tooltip = "Browse",
                    AnchorFrom = BoxAlignment.BottomRight,
                    AnchorTo = BoxAlignment.BottomRight,
                    CanGrow = false
                }
            ]
        });

        textbox.OnValueChanged += (sender, e) => OnValueChanged?.Invoke(this, EventArgs.Empty);
        textbox.OnValueCommited += (sender, e) => OnValueCommited?.Invoke(this, EventArgs.Empty);
        button.OnClick += (sender, e) =>
        {
            switch (mode)
            {
                case PathSelectorMode.Folder:
                    Manager.ScreenLayerManager.OpenFolderPicker(LabelText, textbox.Value, (path) => textbox.Value = path);
                    break;

                case PathSelectorMode.OpenFile:
                    Manager.ScreenLayerManager.OpenFilePicker(LabelText, textbox.Value, null, Filter, (path) => textbox.Value = path);
                    break;

                case PathSelectorMode.OpenDirectory:
                    Manager.ScreenLayerManager.OpenFilePicker(LabelText, "", textbox.Value, Filter, (path) => textbox.Value = path);
                    break;

                case PathSelectorMode.SaveFile:
                    Manager.ScreenLayerManager.OpenSaveLocationPicker(LabelText, textbox.Value, SaveExtension, Filter, (path) => textbox.Value = path);
                    break;
            }
        };
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<PathSelectorStyle>(BuildStyleName());

    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var pathSelectorStyle = (PathSelectorStyle)style;

        layout.StyleName = pathSelectorStyle.LinearLayoutStyle;
        textbox.StyleName = pathSelectorStyle.TextboxStyle;
        button.StyleName = pathSelectorStyle.ButtonStyle;
    }
    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }
}
public enum PathSelectorMode
{
    Folder, OpenFile, OpenDirectory, SaveFile
}