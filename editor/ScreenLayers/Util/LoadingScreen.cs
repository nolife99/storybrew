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

public class LoadingScreen(scoped ReadOnlySpan<char> title, Func<Task> action) : UiScreenLayer
{
    readonly Func<Task> action = action;
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
                        using var sb = ValueList.Create(e.Message.AsSpan());
                        sb.AddRange(" (".AsSpan());
                        sb.AddRange(e.GetType().Name.AsSpan());
                        sb.AddRange(")\n".AsSpan());

                        var innerEx = e.InnerException;
                        while (innerEx is not null)
                        {
                            sb.AddRange("Caused by: ".AsSpan());
                            sb.AddRange(innerEx.Message.AsSpan());
                            sb.AddRange(" (".AsSpan());
                            sb.AddRange(innerEx.GetType().Name.AsSpan());
                            sb.AddRange(")\n ".AsSpan());

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
        tempTitle.AddRange("...".AsSpan());

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