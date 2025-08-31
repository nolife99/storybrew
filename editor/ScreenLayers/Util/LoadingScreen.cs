namespace StorybrewEditor.ScreenLayers.Util;

using System;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.UserInterface;
using BrewLib.Util;
using SDL3;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

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
                    SDL.LogError(SDL.LogCategory.Application,
                        $"{loadingScreen.title.AsReadOnlySpan()} failed ({loadingScreen.action.Method.Name}): {e}");

                    await Program.Schedule(state =>
                        {
                            var (l, ex) = state;

                            using var sb = StringHelper.Interpolate(
                                $"{l.title.AsReadOnlySpan()} failed:\n \n{ex.Message} ({ex.GetType().Name})\n");

                            var innerEx = ex.InnerException;
                            while (innerEx is not null)
                            {
                                sb.Append($"Caused by: {innerEx.Message} ({innerEx.GetType().Name})\n");

                                innerEx = innerEx.InnerException;
                            }

                            sb.Append($"\n \nDetails:\n{ex.GetBaseException()}");

                            l.Manager.ShowMessage(sb.AsReadOnlySpan());

                            l.Exit();
                        },
                        (loadingScreen, e));
                }
            },
            this,
            true);

        base.Load();

        using var tempTitle = StringHelper.Interpolate($"{title.AsReadOnlySpan()}...");
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