namespace StorybrewEditor.ScreenLayers.Util;

using System;
using BrewLib.UserInterface;
using BrewLib.Util;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

public class MessageBox(string message, Action yesAction, Action noAction, bool cancelable) : UiScreenLayer
{
    LinearLayout mainLayout, buttonsLayout;

    public override bool IsPopup => true;

    public override void Load()
    {
        base.Load();
        WidgetManager.Root.Add(mainLayout = new LinearLayout(WidgetManager)
        {
            StyleName = "panel",
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            Children =
            [
                new ScrollArea(WidgetManager,
                    new Label(WidgetManager) { Text = message, AnchorFrom = BoxAlignment.Centre })
                {
                    ScrollsHorizontally = true
                },
                buttonsLayout = new(WidgetManager) { Horizontal = true, AnchorFrom = BoxAlignment.Centre }
            ]
        });

        Button yesButton = new(WidgetManager) { Text = noAction is null ? "Ok" : "Yes", AnchorFrom = BoxAlignment.Centre };

        yesButton.OnClick += (_, _) =>
        {
            Exit();
            yesAction?.Invoke();
        };

        buttonsLayout.Add(yesButton);

        if (noAction is not null)
        {
            Button noButton = new(WidgetManager) { Text = "No", AnchorFrom = BoxAlignment.Centre };

            noButton.OnClick += (_, _) =>
            {
                Exit();
                noAction.Invoke();
            };

            buttonsLayout.Add(noButton);
        }

        if (cancelable)
        {
            Button cancelButton = new(WidgetManager) { Text = "Cancel", AnchorFrom = BoxAlignment.Centre };

            cancelButton.OnClick += (_, _) => Exit();
            buttonsLayout.Add(cancelButton);
        }
    }
    public override bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (e.IsRepeat || e.Key is not Keys.C || !e.Control) return base.OnKeyDown(e);
        ClipboardHelper.SetText(message);
        return true;
    }
    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(400, 0, 1024 - 32, 768 - 32);
    }
}