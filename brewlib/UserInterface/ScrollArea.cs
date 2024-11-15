namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using Graphics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Util;

public class ScrollArea : Widget
{
    readonly StackLayout scrollContainer;
    readonly Label scrollIndicatorTop, scrollIndicatorBottom, scrollIndicatorLeft, scrollIndicatorRight;
    bool dragged, hovered, scrollsHorizontally, scrollsVertically = true;

    public ScrollArea(WidgetManager manager, Widget scrollable) : base(manager)
    {
        ClipChildren = true;
        Add(scrollContainer = new(manager) { FitChildren = true, Children = [scrollable] });

        Add(scrollIndicatorTop = new(manager)
        {
            StyleName = "icon",
            Icon = IconFont.ArrowCircleUp,
            AnchorFrom = BoxAlignment.TopRight,
            AnchorTo = BoxAlignment.TopRight,
            Hoverable = false,
            Opacity = .6f
        });

        Add(scrollIndicatorBottom = new(manager)
        {
            StyleName = "icon",
            Icon = IconFont.ArrowCircleDown,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.BottomRight,
            Hoverable = false,
            Opacity = .6f
        });

        Add(scrollIndicatorLeft = new(manager)
        {
            StyleName = "icon",
            Icon = IconFont.ArrowCircleLeft,
            AnchorFrom = BoxAlignment.BottomLeft,
            AnchorTo = BoxAlignment.BottomLeft,
            Hoverable = false,
            Opacity = .6f
        });

        Add(scrollIndicatorRight = new(manager)
        {
            StyleName = "icon",
            Icon = IconFont.ArrowCircleRight,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.BottomRight,
            Hoverable = false,
            Opacity = .6f
        });

        OnHovered += (_, e) =>
        {
            hovered = e.Hovered;
            updateScrollIndicators();
        };

        OnClickDown += (_, e) =>
        {
            if (e.Button != MouseButton.Left) return false;
            dragged = true;
            return true;
        };

        OnClickUp += (_, e) =>
        {
            if (e.Button != MouseButton.Left) return;
            dragged = false;
        };

        OnClickMove += (_, e) =>
        {
            if (!dragged) return;
            scroll(e.DeltaX, e.DeltaY);
        };

        OnMouseWheel += (_, e) =>
        {
            if (scrollsVertically) scroll(0, e.OffsetY * 64);
            else if (scrollsHorizontally) scroll(e.OffsetY * 64, 0);

            return true;
        };

        scrollIndicatorTop.Pack();
        scrollIndicatorBottom.Pack();
        scrollIndicatorLeft.Pack();
        scrollIndicatorRight.Pack();
        updateScrollIndicators();
    }

    public override Vector2 MinSize => Vector2.Zero;
    public override Vector2 PreferredSize => scrollContainer.PreferredSize;

    public bool ScrollsVertically
    {
        get => scrollsVertically;
        set
        {
            if (scrollsVertically == value) return;
            scrollsVertically = value;
            updateScrollIndicators();
        }
    }

    public bool ScrollsHorizontally
    {
        get => scrollsHorizontally;
        set
        {
            if (scrollsHorizontally == value) return;
            scrollsHorizontally = value;
            updateScrollIndicators();
        }
    }

    float ScrollableX => Math.Max(0, scrollContainer.Width - Width);
    float ScrollableY => Math.Max(0, scrollContainer.Height - Height);

    protected override void DrawChildren(DrawContext drawContext, float actualOpacity)
    {
        scroll(0, 0);
        base.DrawChildren(drawContext, actualOpacity);
    }
    protected override void Layout()
    {
        base.Layout();
        var width = scrollsHorizontally ? Math.Max(Size.X, scrollContainer.PreferredSize.X) : Size.X;
        var height = scrollsVertically ? Math.Max(Size.Y, scrollContainer.PreferredSize.Y) : Size.Y;
        scrollContainer.Size = new(width, height);
    }
    void scroll(float x, float y)
    {
        if (!scrollsHorizontally) x = 0;
        if (!scrollsVertically) y = 0;

        scrollContainer.Offset = new(Math.Max(-ScrollableX, Math.Min(scrollContainer.Offset.X + x, 0)),
            Math.Max(-ScrollableY, Math.Min(scrollContainer.Offset.Y + y, 0)));

        updateScrollIndicators();
    }
    void updateScrollIndicators()
    {
        scrollIndicatorTop.Displayed = hovered && scrollsVertically && scrollContainer.Offset.Y < 0;
        scrollIndicatorBottom.Displayed = hovered && scrollsVertically && scrollContainer.Offset.Y > -ScrollableY;
        scrollIndicatorLeft.Displayed = hovered && scrollsHorizontally && scrollContainer.Offset.X < 0;
        scrollIndicatorRight.Displayed = hovered && scrollsHorizontally && scrollContainer.Offset.X > -ScrollableX;

        scrollIndicatorBottom.Offset = scrollIndicatorRight.Displayed ? new(0, -scrollIndicatorRight.Height) : Vector2.Zero;
        scrollIndicatorRight.Offset = scrollIndicatorBottom.Displayed ? new(-scrollIndicatorBottom.Width, 0) : Vector2.Zero;
    }
}