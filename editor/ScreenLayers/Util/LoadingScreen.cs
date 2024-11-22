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

            if (exception is null)
            {
                Program.Schedule(Exit);
                return;
            }

            Trace.TraceError($"{title} failed ({action.Method.Name}): {exception}");

            var sb = StringHelper.StringBuilderPool.Get();
            sb.Append(exception.Message);
            sb.Append(" (");
            sb.Append(exception.GetType().Name);
            sb.AppendLine(")");

            var innerException = exception.InnerException;
            while (innerException is not null)
            {
                sb.Append("Caused by: ");
                sb.Append(innerException.Message);
                sb.Append(" (");
                sb.Append(innerException.GetType().Name);
                sb.AppendLine(")");

                innerException = innerException.InnerException;
            }

            Program.Schedule(() =>
            {
                Manager.ShowMessage($"{title} failed:\n\n{sb}\n\nDetails:\n{exception.GetBaseException()}");
                StringHelper.StringBuilderPool.Return(sb);
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
            Children = [new Label(WidgetManager) { Text = title + "..." }]
        });
    }
    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(1024);
    }
    public override void Close() { }
}