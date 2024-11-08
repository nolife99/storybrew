namespace StorybrewEditor.ScreenLayers.Util;

using System;
using System.Linq;
using System.Collections.Generic;
using BrewLib.UserInterface;
using BrewLib.Util;

public class ContextMenu<T> : UiScreenLayer
{
    readonly Action<T> callback;
    readonly List<Option> options = [];
    readonly string title;
    Button cancelButton;

    LinearLayout mainLayout, optionsLayout;
    Textbox searchTextbox;

    public ContextMenu(string title, Action<T> callback, params Option[] options)
    {
        this.title = title;
        this.callback = callback;
        this.options.AddRange(options);
    }
    public ContextMenu(string title, Action<T> callback, params T[] options)
    {
        this.title = title;
        this.callback = callback;
        this.options.AddRange(options.Select(option => new(option.ToString(), option)));
    }
    public ContextMenu(string title, Action<T> callback, IEnumerable<T> options)
    {
        this.title = title;
        this.callback = callback;
        this.options.AddRange(options.Select(option => new(option.ToString(), option)));
    }

    public override bool IsPopup => true;

    public override void Load()
    {
        base.Load();

        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            StyleName = "panel",
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            FitChildren = true,
            Children =
            [
                new LinearLayout(WidgetManager)
                {
                    Horizontal = true,
                    Fill = true,
                    Children =
                    [
                        new Label(WidgetManager) { Text = title },
                        searchTextbox = new(WidgetManager)
                        {
                            AnchorFrom = BoxAlignment.Centre, DefaultSize = new(120, 0)
                        },
                        cancelButton = new(WidgetManager)
                        {
                            StyleName = "icon",
                            Icon = IconFont.Cancel,
                            AnchorFrom = BoxAlignment.Centre,
                            CanGrow = false
                        }
                    ]
                },
                new ScrollArea(WidgetManager, optionsLayout = new(WidgetManager) { FitChildren = true })
            ]
        });
        cancelButton.OnClick += (_, _) => Exit();

        searchTextbox.OnValueChanged += (_, _) => refreshOptions();
        refreshOptions();
    }
    void refreshOptions()
    {
        optionsLayout.ClearWidgets();
        foreach (var option in options)
        {
            if (!string.IsNullOrEmpty(searchTextbox.Value) &&
                !option.Name.Contains(searchTextbox.Value, StringComparison.Ordinal))
                continue;

            Button button;
            optionsLayout.Add(button = new(WidgetManager)
            {
                StyleName = "small", Text = option.Name, AnchorFrom = BoxAlignment.Centre
            });

            var result = option.Value;
            button.OnClick += (_, _) =>
            {
                callback.Invoke(result);
                Exit();
            };
        }
    }
    public override void OnTransitionIn()
    {
        base.OnTransitionIn();
        WidgetManager.KeyboardFocus = searchTextbox;
    }
    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(400, 0, 0, 600);
    }

    public sealed class Option(string name, T value)
    {
        public string Name => name;
        public T Value => value;
    }
}