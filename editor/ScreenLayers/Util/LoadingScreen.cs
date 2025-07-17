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
                    await Program.Schedule(l => l.Exit(), loadingScreen);
                }
                catch (Exception e)
                {
                    Trace.TraceError(
                        $"{loadingScreen.title.AsReadOnlySpan()} failed ({loadingScreen.action.Method.Name}): {e}");

                    await Program.Schedule(state =>
                        {
                            var (l, ex) = state;

                            using var sb = TempList.Create(l.title.AsReadOnlySpan());
                            sb.Append(" failed:\n \n");

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

                            sb.Append("\n \nDetails:\n");
                            sb.Append(ex.GetBaseException().ToString());

                            l.Manager.ShowMessage(sb.AsReadOnlySpan());

                            l.Exit();
                        },
                        (loadingScreen, e));
                }
            },
#pragma warning restore EPC17
#pragma warning restore EPC17
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