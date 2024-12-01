namespace BrewLib.UserInterface;

using System;
using System.Collections.Generic;
using System.Numerics;
using Skinning.Styles;
using Util;

public sealed class LinearLayout(WidgetManager manager) : Widget(manager)
{
    bool fill, fitChildren, horizontal, invalidSizes = true;
    Vector2 minSize, preferredSize;

    FourSide padding;
    float spacing;

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

    public bool Horizontal
    {
        get => horizontal;
        set
        {
            if (horizontal == value) return;
            horizontal = value;
            InvalidateAncestorLayout();
        }
    }

    public float Spacing
    {
        get => spacing;
        set
        {
            if (spacing == value) return;
            spacing = value;
            InvalidateAncestorLayout();
        }
    }

    public FourSide Padding
    {
        get => padding;
        set
        {
            if (padding == value) return;
            padding = value;
            InvalidateAncestorLayout();
        }
    }

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

    public bool Fill
    {
        get => fill;
        set
        {
            if (fill == value) return;
            fill = value;
            InvalidateLayout();
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<LinearLayoutStyle>(StyleName);

    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var layoutStyle = (LinearLayoutStyle)style;

        Spacing = layoutStyle.Spacing;
    }
    public override void InvalidateLayout()
    {
        base.InvalidateLayout();
        invalidSizes = true;
    }
    protected override void Layout()
    {
        base.Layout();

        var innerSize = new Vector2(Size.X - padding.Horizontal, Size.Y - padding.Vertical);
        var totalSpace = horizontal ? innerSize.X : innerSize.Y;
        var usedSpace = 0f;

        List<LayoutItem> items = [];
        foreach (var child in Children)
        {
            if (child.AnchorTarget is not null) continue;

            var preferredSize = child.PreferredSize;
            var length = horizontal ? preferredSize.X : preferredSize.Y;

            items.Add(new()
            {
                Widget = child,
                PreferredSize = preferredSize,
                MinSize = child.MinSize,
                MaxSize = child.MaxSize,
                Length = length,
                Scalable = true
            });

            usedSpace += length;
        }

        var totalSpacing = spacing * (items.Count - 1);
        usedSpace += totalSpacing;

        var scalableItems = items.Count;
        while (scalableItems > 0 && Math.Abs(totalSpace - usedSpace) > .001f)
        {
            var remainingSpace = totalSpace - usedSpace;
            if (!fill && remainingSpace > 0) break;

            var adjustment = remainingSpace / scalableItems;
            usedSpace = totalSpacing;
            scalableItems = 0;

            foreach (var item in items)
            {
                if (!item.Widget.CanGrow && adjustment > 0) item.Scalable = false;
                if (item.Scalable)
                {
                    item.Length += adjustment;
                    if (horizontal)
                    {
                        if (item.Length < item.MinSize.X)
                        {
                            item.Length = item.MinSize.X;
                            item.Scalable = false;
                        }
                        else if (item.MaxSize.X > 0 && item.Length >= item.MaxSize.X)
                        {
                            item.Length = item.MaxSize.X;
                            item.Scalable = false;
                        }
                        else ++scalableItems;
                    }
                    else
                    {
                        if (item.Length < item.MinSize.Y)
                        {
                            item.Length = item.MinSize.Y;
                            item.Scalable = false;
                        }
                        else if (item.MaxSize.Y > 0 && item.Length >= item.MaxSize.Y)
                        {
                            item.Length = item.MaxSize.Y;
                            item.Scalable = false;
                        }
                        else ++scalableItems;
                    }
                }

                usedSpace += item.Length;
            }
        }

        var distance = horizontal ? padding.Left : padding.Top;
        foreach (var item in items)
        {
            var child = item.Widget;
            var minSize = item.MinSize;
            var maxSize = item.MaxSize;

            if (horizontal)
            {
                var childBreadth = fitChildren ?
                    Math.Max(minSize.Y, innerSize.Y) :
                    Math.Max(minSize.Y, Math.Min(item.PreferredSize.Y, innerSize.Y));

                if (maxSize.Y > 0 && childBreadth > maxSize.Y) childBreadth = maxSize.Y;

                var anchor = child.AnchorFrom & BoxAlignment.Vertical | BoxAlignment.Left;
                PlaceChildren(child, new(distance, padding.GetVerticalOffset(anchor)), new(item.Length, childBreadth), anchor);
            }
            else
            {
                var childBreadth = fitChildren ?
                    Math.Max(minSize.X, innerSize.X) :
                    Math.Max(minSize.X, Math.Min(item.PreferredSize.X, innerSize.X));

                if (maxSize.X > 0 && childBreadth > maxSize.X) childBreadth = maxSize.X;

                var anchor = child.AnchorFrom & BoxAlignment.Horizontal | BoxAlignment.Top;
                PlaceChildren(child, new(padding.GetHorizontalOffset(anchor), distance), new(childBreadth, item.Length), anchor);
            }

            distance += item.Length + spacing;
        }
    }

    static void PlaceChildren(Widget widget, Vector2 offset, Vector2 size, BoxAlignment anchor)
    {
        widget.Offset = offset;
        widget.Size = size;
        widget.AnchorFrom = anchor;
        widget.AnchorTo = anchor;
    }
    void measureChildren()
    {
        if (!invalidSizes) return;
        invalidSizes = false;

        float width = 0, height = 0, minWidth = 0, minHeight = 0;

        var firstChild = true;
        foreach (var child in Children)
        {
            if (child.AnchorTarget is not null) continue;

            var childMinSize = child.MinSize;
            var childSize = child.PreferredSize;

            if (horizontal)
            {
                height = Math.Max(height, childSize.Y);
                width += childSize.X;

                minHeight = Math.Max(minHeight, childMinSize.Y);
                minWidth += childMinSize.X;

                if (!firstChild)
                {
                    width += spacing;
                    minWidth += spacing;
                }
            }
            else
            {
                width = Math.Max(width, childSize.X);
                height += childSize.Y;

                minWidth = Math.Max(minWidth, childMinSize.X);
                minHeight += childMinSize.Y;

                if (!firstChild)
                {
                    height += spacing;
                    minHeight += spacing;
                }
            }

            firstChild = false;
        }

        var paddingH = padding.Horizontal;
        var paddingV = padding.Vertical;

        minSize = new(minWidth + paddingH, minHeight + paddingV);
        preferredSize = new(width + paddingH, height + paddingV);
    }

    sealed record LayoutItem
    {
        public float Length;
        public Vector2 PreferredSize, MinSize, MaxSize;
        public bool Scalable;
        public Widget Widget;
    }
}