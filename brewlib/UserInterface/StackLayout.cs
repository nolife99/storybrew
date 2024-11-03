using System;
using System.Numerics;
using BrewLib.UserInterface.Skinning.Styles;

namespace BrewLib.UserInterface;

public class StackLayout(WidgetManager manager) : Widget(manager)
{
    Vector2 minSize, preferredSize;
    bool invalidSizes = true;

    public override Vector2 MinSize
    {
        get
        {
            measureChildren();
            return minSize;
        }
    }
    public override Vector2 PreferredSize
    {
        get
        {
            measureChildren();
            return preferredSize;
        }
    }

    bool fitChildren;
    public bool FitChildren
    {
        get => fitChildren;
        set
        {
            if (fitChildren == value) return;
            fitChildren = value;
            InvalidateLayout();
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<StackLayoutStyle>(StyleName);
    public override void InvalidateLayout()
    {
        base.InvalidateLayout();
        invalidSizes = true;
    }
    protected override void Layout()
    {
        base.Layout();

        foreach (var child in Children)
        {
            if (child.AnchorTarget is not null) continue;
            if (child.AnchorFrom != child.AnchorTo) continue;

            var preferredSize = child.PreferredSize;
            var minSize = child.MinSize;
            var maxSize = child.MaxSize;

            var childWidth = fitChildren ? Math.Max(minSize.X, Size.X) : Math.Max(minSize.X, Math.Min(preferredSize.X, Size.X));
            if (maxSize.X > 0 && childWidth > maxSize.X) childWidth = maxSize.X;

            var childHeight = fitChildren ? Math.Max(minSize.Y, Size.Y) : Math.Max(minSize.Y, Math.Min(preferredSize.Y, Size.Y));
            if (maxSize.Y > 0 && childHeight > maxSize.Y) childHeight = maxSize.Y;

            PlaceChildren(child, Vector2.Zero, new(childWidth, childHeight));
        }
    }
    protected virtual void PlaceChildren(Widget widget, Vector2 offset, Vector2 size) => widget.Size = size;

    private void measureChildren()
    {
        if (!invalidSizes) return;
        invalidSizes = false;

        var width = 0f;
        var height = 0f;

        var minWidth = width;
        var minHeight = height;

        foreach (var child in Children)
        {
            if (child.AnchorTarget is not null) continue;

            var childMinSize = child.MinSize;
            var childSize = child.PreferredSize;

            width = Math.Max(width, childSize.X);
            height = Math.Max(height, childSize.Y);

            minWidth = Math.Max(minWidth, childMinSize.X);
            minHeight = Math.Max(minHeight, childMinSize.Y);
        }
        minSize = new(minWidth, minHeight);
        preferredSize = new(width, height);
    }
}