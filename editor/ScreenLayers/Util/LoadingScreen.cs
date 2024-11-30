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
            Exception ex = null;
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex is null)
            {
                Program.Schedule(Exit);
                return;
            }

            Trace.TraceError($"{title} failed ({action.Method.Name}): {ex}");

            var sb = StringHelper.StringBuilderPool.Retrieve();
            sb.Append(ex.Message);
            sb.Append(" (");
            sb.Append(ex.GetType().Name);
            sb.Append(")\n");

            var innerEx = ex.InnerException;
            while (innerEx is not null)
            {
                sb.Append("Caused by: ");
                sb.Append(innerEx.Message);
                sb.Append(" (");
                sb.Append(innerEx.GetType().Name);
                sb.Append(")\n ");

                innerEx = innerEx.InnerException;
            }

            Program.Schedule(() =>
            {
                Manager.ShowMessage($"{title} failed:\n \n{sb}\n \nDetails:\n{ex.GetBaseException()}");
                StringHelper.StringBuilderPool.Release(sb);
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
}