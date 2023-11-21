﻿using BrewLib.UserInterface;
using BrewLib.Util;
using osuTK;
using System;
using System.Collections.Generic;

namespace StorybrewEditor.ScreenLayers
{
    public class ContextMenu<T> : UiScreenLayer
    {
        readonly string title;
        readonly Action<T> callback;
        readonly List<Option> options = [];

        LinearLayout mainLayout, optionsLayout;
        Textbox searchTextbox;
        Button cancelButton;

        public override bool IsPopup => true;

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
            foreach (var option in options) this.options.Add(new Option(option.ToString(), option));
        }
        public ContextMenu(string title, Action<T> callback, IEnumerable<T> options)
        {
            this.title = title;
            this.callback = callback;
            foreach (var option in options) this.options.Add(new Option(option.ToString(), option));
        }

        public override void Load()
        {
            base.Load();

            WidgetManager.Root.Add(mainLayout = new LinearLayout(WidgetManager)
            {
                StyleName = "panel",
                AnchorTarget = WidgetManager.Root,
                AnchorFrom = BoxAlignment.Centre,
                AnchorTo = BoxAlignment.Centre,
                Padding = new FourSide(16),
                FitChildren = true,
                Children = new Widget[]
                {
                    new LinearLayout(WidgetManager)
                    {
                        Horizontal = true,
                        Fill = true,
                        Children = new Widget[]
                        {
                            new Label(WidgetManager)
                            {
                                Text = title
                            },
                            searchTextbox = new Textbox(WidgetManager)
                            {
                                AnchorFrom = BoxAlignment.Centre,
                                DefaultSize = new Vector2(120, 0)
                            },
                            cancelButton = new Button(WidgetManager)
                            {
                                StyleName = "icon",
                                Icon = IconFont.TimesCircle,
                                AnchorFrom = BoxAlignment.Centre,
                                CanGrow = false
                            }
                        }
                    },
                    new ScrollArea(WidgetManager, optionsLayout = new LinearLayout(WidgetManager)
                    {
                        FitChildren = true
                    })
                }
            });
            cancelButton.OnClick += (sender, e) => Exit();

            searchTextbox.OnValueChanged += (sender, e) => refreshOptions();
            refreshOptions();
        }
        void refreshOptions()
        {
            optionsLayout.ClearWidgets();
            foreach (var option in options)
            {
                if (!string.IsNullOrEmpty(searchTextbox.Value) && !option.Name.Contains(searchTextbox.Value, StringComparison.InvariantCultureIgnoreCase)) continue;

                Button button;
                optionsLayout.Add(button = new Button(WidgetManager)
                {
                    StyleName = "small",
                    Text = option.Name,
                    AnchorFrom = BoxAlignment.Centre,
                });

                var result = option.Value;
                button.OnClick += (sender, e) =>
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
        public readonly struct Option(string name, T value)
        {
            public readonly string Name = name;
            public readonly T Value = value;
        }
    }
}