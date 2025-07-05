namespace StorybrewEditor.ScreenLayers.Util;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.UserInterface;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class LoadingScreen(scoped ReadOnlySpan<char> title, Func<ValueTask> action) : UiScreenLayer
{
    readonly Func<ValueTask> action = action;
    LinearLayout mainLayout;
    ValueArray<char> title = ValueArray.Create(title);

    public override bool IsPopup => true;

    public override void Load()
    {
        ThreadPool.UnsafeQueueUserWorkItem(async loadingScreen =>
            {
                try
                {
                    await loadingScreen.action();
                    await Program.Schedule(loadingScreen.Exit);
                }
                catch (Exception e)
                {
                    Trace.TraceError(
                        $"{loadingScreen.title.AsReadOnlySpan()} failed ({loadingScreen.action.Method.Name}): {e}");

                    await Program.Schedule(() =>
                    {
                        using var sb = TempList.Create(e.Message);
                        sb.Append(" (");
                        sb.Append(e.GetType().Name);
                        sb.Append(")\n");

                        var innerEx = e.InnerException;
                        while (innerEx is not null)
                        {
                            sb.Append("Caused by: ");
                            sb.Append(innerEx.Message);
                            sb.Append(" (");
                            sb.Append(innerEx.GetType().Name);
                            sb.Append(")\n ");

                            innerEx = innerEx.InnerException;
                        }

                        loadingScreen.Manager.ShowMessage(
                            $"{loadingScreen.title.AsReadOnlySpan()} failed:\n \n{sb.AsReadOnlySpan()}\n \nDetails:\n{e.GetBaseException()}");

                        loadingScreen.Exit();
                    });
                }
            },
            this,
            true);

        base.Load();

        using var tempTitle = TempList.Create(title.AsReadOnlySpan());
        tempTitle.Append("...");

        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Bottom,
            AnchorTo = BoxAlignment.Bottom,
            Offset = new(0, -64),
            Padding = new(16),
            FitChildren = true,
            Horizontal = true,
            Children = [new Label(WidgetManager) { Text = tempTitle.AsReadOnlySpan() }]
        });
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(1024);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) title.Dispose();
        base.Dispose(disposing);
    }
}