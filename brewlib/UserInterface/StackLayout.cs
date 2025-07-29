namespace BrewLib.UserInterface;

using System.Numerics;
using BrewLib.UserInterface.Skinning.Styles;

public sealed class StackLayout(WidgetManager manager) : Widget(manager)
{
    bool fitChildren, invalidSizes = true;
    Vector2 minSize, preferredSize;

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

    protected override void InvalidateLayout()
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

            var childWidth = fitChildren ?
                float.Max(minSize.X, Size.X) :
                float.Max(minSize.X, float.Min(preferredSize.X, Size.X));

            if (maxSize.X > 0 && childWidth > maxSize.X) childWidth = maxSize.X;

            var childHeight = fitChildren ?
                float.Max(minSize.Y, Size.Y) :
                float.Max(minSize.Y, float.Min(preferredSize.Y, Size.Y));

            if (maxSize.Y > 0 && childHeight > maxSize.Y) childHeight = maxSize.Y;

            child.Size = new(childWidth, childHeight);
        }
    }

    void measureChildren()
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

            width = float.Max(width, childSize.X);
            height = float.Max(height, childSize.Y);

            minWidth = float.Max(minWidth, childMinSize.X);
            minHeight = float.Max(minHeight, childMinSize.Y);
        }

        minSize = new(minWidth, minHeight);
        preferredSize = new(width, height);
    }
}