using System;
using BrewLib.UserInterface;
using BrewLib.Util;
using osuTK.Input;

namespace StorybrewEditor.ScreenLayers.Util;

public class MessageBox(string message, Action yesAction, Action noAction, bool cancelable) : UiScreenLayer
{
    LinearLayout mainLayout, buttonsLayout;
    public override bool IsPopup => true;

    public MessageBox(string message, Action okAction = null) : this(message, okAction, null, false) { }
    public MessageBox(string message, Action okAction, bool cancelable) : this(message, okAction, null, cancelable) { }

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
                new ScrollArea(WidgetManager, new Label(WidgetManager)
                {
                    Text = message,
                    AnchorFrom = BoxAlignment.Centre
                })
                {
                    ScrollsHorizontally = true
                },
                buttonsLayout = new(WidgetManager)
                {
                    Horizontal = true,
                    AnchorFrom = BoxAlignment.Centre
                }
            ]
        });
        Button yesButton = new(WidgetManager)
        {
            Text = noAction is not null ? "Yes" : "Ok",
            AnchorFrom = BoxAlignment.Centre
        };
        yesButton.OnClick += (_, _) =>
        {
            Exit();
            yesAction?.Invoke();
        };
        buttonsLayout.Add(yesButton);

        if (noAction is not null)
        {
            Button noButton = new(WidgetManager)
            {
                Text = "No",
                AnchorFrom = BoxAlignment.Centre
            };
            noButton.OnClick += (_, _) =>
            {
                Exit();
                noAction.Invoke();
            };
            buttonsLayout.Add(noButton);
        }
        if (cancelable)
        {
            Button cancelButton = new(WidgetManager)
            {
                Text = "Cancel",
                AnchorFrom = BoxAlignment.Centre
            };
            cancelButton.OnClick += (_, _) => Exit();
            buttonsLayout.Add(cancelButton);
        }
    }
    public override bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        if (!e.IsRepeat && e.Key is Key.C && e.Control)
        {
            ClipboardHelper.SetText(message);
            return true;
        }
        return base.OnKeyDown(e);
    }
    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(400, 0, 1024 - 32, 768 - 32);
    }
}