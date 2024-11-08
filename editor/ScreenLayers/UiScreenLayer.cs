namespace StorybrewEditor.ScreenLayers;

using System;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.ScreenLayers;
using BrewLib.UserInterface;

public class UiScreenLayer : ScreenLayer
{
    float opacity;
    CameraOrtho uiCamera;
    protected WidgetManager WidgetManager { get; private set; }

    public override void Load()
    {
        base.Load();

        var editor = Manager.GetContext<Editor>();
        AddInputHandler(WidgetManager = new(Manager, editor.InputManager, editor.Skin) { Camera = uiCamera = new() });
    }

    public override void Resize(int width, int height)
    {
        uiCamera.VirtualHeight = (int)(height * Math.Max(1024f / width, 768f / height));
        uiCamera.VirtualWidth = width * uiCamera.VirtualHeight / height;
        WidgetManager.Size = new(uiCamera.VirtualWidth, uiCamera.VirtualHeight);
        base.Resize(width, height);
    }

    public override void Update(bool isTop, bool isCovered)
    {
        base.Update(isTop, isCovered);

        if (Manager.GetContext<Editor>().IsFixedRateUpdate)
        {
            var targetOpacity = isTop ? 1 : .3f;
            opacity = Math.Abs(opacity - targetOpacity) <= .07f ? targetOpacity
                : Math.Clamp(opacity + (opacity < targetOpacity ? .07f : -.07f), 0, 1);
        }

        WidgetManager.Opacity = opacity * TransitionProgress;
    }

    public override void Draw(DrawContext drawContext, double tween)
    {
        base.Draw(drawContext, tween);
        WidgetManager.Draw(drawContext);
    }

    protected static void MakeTabs(Button[] buttons, Widget[] widgets)
    {
        for (var i = 0; i < buttons.Length; ++i)
        {
            var button = buttons[i];
            var widget = widgets[i];

            button.Checkable = true;
            widget.Displayed = button.Checked;

            button.OnValueChanged += (sender, _) =>
            {
                if (!(widget.Displayed = button.Checked)) return;
                foreach (var otherButton in buttons)
                    if (sender != otherButton && otherButton.Checked)
                        otherButton.Checked = false;
            };
        }
    }

#region IDisposable Support

    bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                WidgetManager.Dispose();
                uiCamera.Dispose();
                disposed = true;
            }

            WidgetManager = null;
            uiCamera = null;
        }

        base.Dispose(disposing);
    }

#endregion
}