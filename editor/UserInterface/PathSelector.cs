namespace StorybrewEditor.UserInterface;

using System;
using System.Collections.Generic;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using ScreenLayers;
using Skinning.Styles;

public class PathSelector : Widget
{
    const string SaveExtension = "";
    readonly Button button;
    readonly LinearLayout layout;
    readonly Textbox textbox;

    public IReadOnlyCollection<KeyValuePair<string, string>> Filter;

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
                textbox = new(manager) { AnchorFrom = BoxAlignment.BottomLeft, AnchorTo = BoxAlignment.BottomLeft },
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

        textbox.OnValueChanged += (_, _) => OnValueChanged?.Invoke(this, EventArgs.Empty);
        textbox.OnValueCommited += (_, _) => OnValueCommited?.Invoke(this, EventArgs.Empty);
        button.OnClick += (_, _) =>
        {
            switch (mode)
            {
                case PathSelectorMode.Folder:
                    Manager.ScreenLayerManager.OpenFolderPicker(textbox.Value, path => textbox.Value = path); break;

                case PathSelectorMode.OpenFile:
                    Manager.ScreenLayerManager.OpenFilePicker(textbox.Value, "", Filter, path => textbox.Value = path);
                    break;

                case PathSelectorMode.OpenDirectory:
                    Manager.ScreenLayerManager.OpenFilePicker("", textbox.Value, Filter, path => textbox.Value = path);
                    break;

                case PathSelectorMode.SaveFile:
                    Manager.ScreenLayerManager.OpenSaveLocationPicker(textbox.Value,
                        SaveExtension,
                        Filter,
                        path => textbox.Value = path); break;
            }
        };
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public string LabelText { get => textbox.LabelText; init => textbox.LabelText = value; }

    public string Value { get => textbox.Value; set => textbox.Value = value; }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<PathSelectorStyle>(BuildStyleName());

    public event EventHandler OnValueChanged, OnValueCommited;

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