﻿namespace StorybrewEditor.ScreenLayers.Util;

using System;
using BrewLib.UserInterface;
using BrewLib.Util;

public class PromptBox(string title, string description, string initialText, Action<string> action) : UiScreenLayer
{
    LinearLayout mainLayout;
    Button okButton, cancelButton;
    Textbox textbox;

    public override bool IsPopup => true;

    public override void Load()
    {
        base.Load();

        Label descriptionLabel;
        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            StyleName = "panel",
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            Children =
            [
                descriptionLabel = new(WidgetManager)
                {
                    StyleName = "small", Text = description, AnchorFrom = BoxAlignment.Centre
                },
                textbox = new(WidgetManager)
                {
                    LabelText = title, AnchorFrom = BoxAlignment.Centre, Value = initialText
                },
                new LinearLayout(WidgetManager)
                {
                    Horizontal = true,
                    AnchorFrom = BoxAlignment.Centre,
                    Children =
                    [
                        okButton = new(WidgetManager) { Text = "Ok", AnchorFrom = BoxAlignment.Centre },
                        cancelButton = new(WidgetManager)
                        {
                            Text = "Cancel", AnchorFrom = BoxAlignment.Centre
                        }
                    ]
                }
            ]
        });

        if (string.IsNullOrWhiteSpace(description)) descriptionLabel.Dispose();

        okButton.OnClick += (_, _) =>
        {
            Exit();
            action?.Invoke(textbox.Value);
        };

        cancelButton.OnClick += (_, _) => Exit();
    }

    public override void OnTransitionIn()
    {
        base.OnTransitionIn();

        WidgetManager.KeyboardFocus = textbox;
        textbox.SelectAll();
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(400);
    }
}