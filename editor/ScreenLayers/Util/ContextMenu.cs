namespace StorybrewEditor.ScreenLayers.Util;

using System;
using System.Collections.Generic;
using System.Linq;
using BrewLib.UserInterface;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public class ContextMenu<T> : UiScreenLayer
{
    readonly Action<T> callback;

    Button cancelButton;
    LinearLayout mainLayout, optionsLayout;
    ValueList<Option> options;
    Textbox searchTextbox;
    ValueArray<char> title;

    public ContextMenu(scoped ReadOnlySpan<char> title, Action<T> callback, params ReadOnlySpan<T> options)
    {
        this.title = ValueArray.Create(title);
        this.callback = callback;

        this.options = ValueList.Create<Option>(options.Length);
        foreach (var option in options) this.options.Add(new(option.ToString(), option));
    }

    public ContextMenu(scoped ReadOnlySpan<char> title, Action<T> callback, IEnumerable<T> options)
    {
        this.title = ValueArray.Create(title);
        this.callback = callback;
        this.options = ValueList.Create(options.Select(option => new Option(option.ToString(), option)));
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
                        new Label(WidgetManager) { Text = title.AsReadOnlySpan() },
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
            if (!searchTextbox.Value.IsEmpty &&
                !option.Name.AsSpan().Contains(searchTextbox.Value, StringComparison.Ordinal)) continue;

            Button button = new(WidgetManager)
            {
                StyleName = "small", Text = option.Name, AnchorFrom = BoxAlignment.Centre
            };

            optionsLayout.Add(button);

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        options.Dispose();
        title.Dispose();
    }

    readonly struct Option(string name, T value)
    {
        public readonly string Name = name;
        public readonly T Value = value;
    }
}