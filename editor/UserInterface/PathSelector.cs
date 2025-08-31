namespace StorybrewEditor.UserInterface;

using System;
using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using SDL3;
using StorybrewEditor.ScreenLayers;
using StorybrewEditor.UserInterface.Skinning.Styles;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public class PathSelector : Widget
{
    readonly Button button;
    readonly LinearLayout layout;
    readonly Textbox textbox;

    ValueArray<DialogFileFilter> filter;

    public PathSelector(WidgetManager manager, PathSelectorMode mode, scoped ReadOnlySpan<DialogFileFilter> filters) :
        base(manager)
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
                    AnchorFrom = BoxAlignment.BottomLeft, AnchorTo = BoxAlignment.BottomLeft
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

        textbox.OnValueChanged += (_, _) => OnValueChanged?.Invoke(this, EventArgs.Empty);
        textbox.OnValueCommited += (_, _) => OnValueCommited?.Invoke(this, EventArgs.Empty);

        filter = ValueArray.Create(filters);
        button.OnClick += (_, _) =>
        {
            switch (mode)
            {
                case PathSelectorMode.Folder:
                    Manager.ScreenLayerManager.OpenFolderPicker(textbox.Value, path => textbox.Value = path);
                    break;

                case PathSelectorMode.OpenFile:
                    Manager.ScreenLayerManager.OpenFilePicker(textbox.Value,
                        default,
                        filter.AsReadOnlySpan(),
                        path => textbox.Value = path);

                    break;

                case PathSelectorMode.OpenDirectory:
                    Manager.ScreenLayerManager.OpenFilePicker(default,
                        textbox.Value,
                        filter.AsReadOnlySpan(),
                        path => textbox.Value = path);

                    break;

                case PathSelectorMode.SaveFile:
                    Manager.ScreenLayerManager.OpenSaveLocationPicker(textbox.Value,
                        filter.AsReadOnlySpan(),
                        path => textbox.Value = path);

                    break;
            }
        };
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public ReadOnlySpan<char> LabelText { get => textbox.LabelText; init => textbox.LabelText = value; }

    public ReadOnlySpan<char> Value { get => textbox.Value; set => textbox.Value = value; }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing) filter.Dispose();
        base.Dispose(disposing);
    }
}

public enum PathSelectorMode
{
    Folder, OpenFile, OpenDirectory, SaveFile
}