namespace StorybrewEditor.ScreenLayers.Util;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BrewLib.UserInterface;
using BrewLib.Util;

public class LoadingScreen(string title, Action action) : UiScreenLayer
{
    LinearLayout mainLayout;

    public override bool IsPopup => true;

    public override void Load()
    {
        Task.Run(() =>
        {
            Exception exception = null;
            try
            {
                action();
            }
            catch (Exception e)
            {
                exception = e;
            }

            Program.Schedule(() =>
            {
                if (exception is not null)
                {
                    Trace.TraceError($"{title} failed ({action.Method.Name}): {exception}");

                    var exceptionMessage = $"{exception.Message} ({exception.GetType().Name})";
                    var innerException = exception.InnerException;
                    while (innerException is not null)
                    {
                        exceptionMessage += $"\nCaused by: {innerException.Message} ({innerException.GetType().Name})";
                        innerException = innerException.InnerException;
                    }

                    Manager.ShowMessage($"{title} failed:\n\n{exceptionMessage}\n\nDetails:\n{exception.GetBaseException()}");
                }

                Exit();
            });
        });

        base.Load();
        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Bottom,
            AnchorTo = BoxAlignment.Bottom,
            Offset = new(0, -64),
            Padding = new(16),
            FitChildren = true,
            Horizontal = true,
            Children = [new Label(WidgetManager) { Text = $"{title}..." ?? "Loading..." }]
        });
    }
    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(1024);
    }
    public override void Close() { }
}