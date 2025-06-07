namespace StorybrewEditor.ScreenLayers.Util;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BrewLib.UserInterface;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class LoadingScreen(scoped ReadOnlySpan<char> title, Func<Task> action) : UiScreenLayer
{
    readonly ValueArray<char> title = ValueArray<char>.Create(title);
    LinearLayout mainLayout;

    public override bool IsPopup => true;

    public override void Load()
    {
        Task.Run(async () =>
        {
            Exception ex = null;
            try
            {
                await action();
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex is null)
            {
                await Program.Schedule(Exit);
                return;
            }

            Trace.TraceError($"{title.AsReadOnlySpan()} failed ({action.Method.Name}): {ex}");

            using var sb = ValueList<char>.Create();
            sb.AddRange(ex.Message.AsSpan());
            sb.AddRange(" (".AsSpan());
            sb.AddRange(ex.GetType().Name.AsSpan());
            sb.AddRange(")\n".AsSpan());

            var innerEx = ex.InnerException;
            while (innerEx is not null)
            {
                sb.AddRange("Caused by: ".AsSpan());
                sb.AddRange(innerEx.Message.AsSpan());
                sb.AddRange(" (".AsSpan());
                sb.AddRange(innerEx.GetType().Name.AsSpan());
                sb.AddRange(")\n ".AsSpan());

                innerEx = innerEx.InnerException;
            }

            await Program.Schedule(() =>
            {
                Manager.ShowMessage(
                    $"{title.AsReadOnlySpan()} failed:\n \n{sb.AsReadOnlySpan()}\n \nDetails:\n{ex.GetBaseException()}");

                Exit();
            });
        });

        base.Load();

        using var tempTitle = TempList<char>.Create(title);
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