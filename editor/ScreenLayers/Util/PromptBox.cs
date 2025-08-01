namespace StorybrewEditor.ScreenLayers.Util;

using System;
using BrewLib.UserInterface;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public class PromptBox(scoped ReadOnlySpan<char> title,
    scoped ReadOnlySpan<char> description,
    scoped ReadOnlySpan<char> initialText,
    Action<ReadOnlySpan<char>> action) : UiScreenLayer
{
    ValueArray<char> description = ValueArray.Create(description), initialText = ValueArray.Create(initialText),
        title = ValueArray.Create(title);

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
                    StyleName = "small", Text = description.AsReadOnlySpan(), AnchorFrom = BoxAlignment.Centre
                },
                textbox = new(WidgetManager)
                {
                    LabelText = title.AsReadOnlySpan(),
                    AnchorFrom = BoxAlignment.Centre,
                    Value = initialText.AsReadOnlySpan()
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

        if (description.AsReadOnlySpan().IsWhiteSpace()) descriptionLabel.Dispose();

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        title.Dispose();
        description.Dispose();
        initialText.Dispose();
    }
}