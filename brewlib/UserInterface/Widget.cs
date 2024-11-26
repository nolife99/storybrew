namespace BrewLib.UserInterface;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Graphics;
using Graphics.Drawables;
using OpenTK.Windowing.Common;
using SixLabors.ImageSharp;
using Skinning.Styles;
using Util;

public class Widget(WidgetManager manager) : IDisposable
{
    static int nextId;
    public readonly int Id = nextId++;

    Drawable background = NullDrawable.Instance, foreground = NullDrawable.Instance;
    bool clipChildren, displayed = true, hoverable = true;

    public float Opacity = 1;

    string styleName, tooltip;
    protected WidgetManager Manager => manager;

    public bool Displayed
    {
        get => displayed;
        set
        {
            if (displayed == value) return;
            displayed = value;
            OnDisplayedChanged?.Invoke(this, EventArgs.Empty);
            if (hoverable) manager.RefreshHover();
        }
    }

    public bool Visible => displayed && (Parent is null || Parent.Visible);

    public bool Hoverable
    {
        get => hoverable;
        set
        {
            if (hoverable == value) return;
            hoverable = value;
            if (Visible) manager.RefreshHover();
        }
    }

    protected bool ClipChildren
    {
        get => clipChildren;
        set
        {
            if (clipChildren == value) return;
            clipChildren = value;
            if (Visible && hoverable) manager.RefreshHover();
        }
    }

    public string StyleName
    {
        get => styleName;
        set
        {
            if (styleName == value) return;
            styleName = value;
            RefreshStyle();
        }
    }

    public Drawable Background
    {
        get => background;
        set
        {
            if (background == value) return;
            background = value;
            InvalidateLayout();
        }
    }

    public Drawable Foreground
    {
        get => foreground;
        set
        {
            if (foreground == value) return;
            foreground = value;
            InvalidateLayout();
        }
    }

    public string Tooltip
    {
        get => tooltip;
        set
        {
            if (tooltip == value) return;
            tooltip = value;

            if (string.IsNullOrWhiteSpace(tooltip))
            {
                Manager.UnregisterTooltip(this);
                tooltip = null;
            }
            else Manager.RegisterTooltip(this, tooltip);
        }
    }

    public event EventHandler OnDisplayedChanged;

    public Widget GetWidgetAt(float x, float y)
    {
        if (!displayed || !hoverable) return null;

        var position = AbsolutePosition;
        var overThis = x >= position.X && x < position.X + size.X && y >= position.Y && y < position.Y + size.Y;

        if (ClipChildren && !overThis) return null;

        for (var i = children.Count - 1; i >= 0; i--)
        {
            var result = children[i].GetWidgetAt(x, y);
            if (result is not null) return result;
        }

        return overThis ? this : null;
    }
    public void Draw(DrawContext drawContext, float parentOpacity)
    {
        var actualOpacity = Opacity * parentOpacity;
        DrawBackground(drawContext, actualOpacity);
        DrawChildren(drawContext, actualOpacity);
        DrawForeground(drawContext, actualOpacity);
    }
    protected virtual void DrawBackground(DrawContext drawContext, float actualOpacity)
        => background?.Draw(drawContext, manager.Camera, Bounds, actualOpacity);

    protected virtual void DrawChildren(DrawContext drawContext, float actualOpacity)
    {
        if (children.Count == 0) return;
        using (ClipChildren ? DrawState.Clip(Bounds, Manager.Camera) : null)
            foreach (var child in children)
                if (child.Displayed)
                    child.Draw(drawContext, actualOpacity);
    }
    protected virtual void DrawForeground(DrawContext drawContext, float actualOpacity)
        => foreground?.Draw(drawContext, manager.Camera, Bounds, actualOpacity);

    public override string ToString() => $"{GetType().Name} {StyleName} #{Id} {Width}x{Height}";

    #region Styling

    protected virtual WidgetStyle Style => Manager.Skin.GetStyle<WidgetStyle>(StyleName);

    public void RefreshStyle()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var style = Style;
        if (style is not null) ApplyStyle(style);
    }
    protected virtual void ApplyStyle(WidgetStyle style)
    {
        Background = style.Background;
        Foreground = style.Foreground;
    }

    public string BuildStyleName(params string[] modifiers) => buildStyleName(StyleName, modifiers);
    static string buildStyleName(string baseName, string[] modifiers)
    {
        if (modifiers.Length == 0) return baseName;

        var sb = StringHelper.StringBuilderPool.Get();
        sb.Append(baseName);

        foreach (var modifier in modifiers)
        {
            if (string.IsNullOrEmpty(modifier)) continue;

            sb.Append(" #");
            sb.Append(modifier);
        }

        var str = sb.ToString();
        StringHelper.StringBuilderPool.Return(sb);
        return str;
    }

    #endregion

    #region Parenting

    public Widget Parent { get; private set; }

    readonly List<Widget> children = [];

    public IEnumerable<Widget> Children
    {
        get => children;
        set
        {
            ClearWidgets();
            foreach (var widget in value) Add(widget);
        }
    }

    public void Add(Widget widget)
    {
        if (children.Contains(widget)) return;

        if (widget == this) throw new InvalidOperationException("Cannot parent a widget to itself");
        if (widget.HasDescendant(this)) throw new InvalidOperationException("Cannot recursively parent a widget to itself");

        widget.Parent?.Remove(widget);
        children.Add(widget);
        widget.Parent = this;

        widget.StyleName ??= "default";

        InvalidateAncestorLayout();
    }
    public void Remove(Widget widget)
    {
        if (!children.Contains(widget)) return;

        widget.Parent = null;
        children.Remove(widget);

        InvalidateAncestorLayout();
    }
    public void ClearWidgets()
    {
        foreach (var child in children.ToArray()) child.Dispose();
    }
    public bool HasAncestor(Widget widget)
    {
        if (Parent is null) return false;
        return Parent == widget || Parent.HasAncestor(widget);
    }
    public bool HasDescendant(Widget widget) => children.Exists(c => c == widget || c.HasDescendant(widget));

    public IEnumerable<Widget> GetAncestors()
    {
        for (var ancestor = Parent; ancestor is not null; ancestor = ancestor.Parent) yield return ancestor;
    }

    #endregion

    #region Placement

    Vector2 offset, size, absolutePosition;
    BoxAlignment anchorFrom = BoxAlignment.TopLeft, anchorTo = BoxAlignment.TopLeft;

    public Vector2 Offset
    {
        get => offset;
        set
        {
            if (offset == value) return;
            offset = value;
            manager.InvalidateAnchors();
        }
    }

    public Vector2 Size
    {
        get => size;
        set
        {
            if (size == value) return;
            size = value;
            InvalidateLayout();
        }
    }

    public float Width { get => Size.X; set => Size = Size with { X = value }; }

    public float Height { get => Size.Y; set => Size = Size with { Y = value }; }

    public Vector2 AbsolutePosition
    {
        get
        {
            manager.RefreshAnchors();
            return absolutePosition;
        }
    }

    public RectangleF Bounds => new(AbsolutePosition.X, AbsolutePosition.Y, Size.X, Size.Y);

    Widget anchorTarget;

    public Widget AnchorTarget
    {
        get => anchorTarget;
        set
        {
            if (anchorTarget == value) return;
            anchorTarget = value;
            manager.InvalidateAnchors();
        }
    }

    public BoxAlignment AnchorFrom
    {
        get => anchorFrom;
        set
        {
            if (anchorFrom == value) return;
            anchorFrom = value;
            manager.InvalidateAnchors();
        }
    }

    public BoxAlignment AnchorTo
    {
        get => anchorTo;
        set
        {
            if (anchorTo == value) return;
            anchorTo = value;
            manager.InvalidateAnchors();
        }
    }

    int anchoringIteration;
    public void UpdateAnchoring(int iteration, bool includeChildren = true)
    {
        ValidateLayout();
        if (anchoringIteration < iteration)
        {
            anchoringIteration = iteration;
            var actualAnchorTarget =
                anchorTarget is not null && (anchorTarget.Parent is not null || anchorTarget == manager.Root) ?
                    anchorTarget :
                    Parent;

            if (actualAnchorTarget is not null)
            {
                actualAnchorTarget.UpdateAnchoring(iteration, false);
                absolutePosition = actualAnchorTarget.absolutePosition + offset;

                if ((anchorFrom & BoxAlignment.Right) > 0) absolutePosition.X -= size.X;
                else if ((anchorFrom & BoxAlignment.Left) == 0) absolutePosition.X -= size.X * .5f;

                if ((anchorFrom & BoxAlignment.Bottom) > 0) absolutePosition.Y -= size.Y;
                else if ((anchorFrom & BoxAlignment.Top) == 0) absolutePosition.Y -= size.Y * .5f;

                if ((anchorTo & BoxAlignment.Right) > 0) absolutePosition.X += actualAnchorTarget.Size.X;
                else if ((anchorTo & BoxAlignment.Left) == 0) absolutePosition.X += actualAnchorTarget.Size.X * .5f;

                if ((anchorTo & BoxAlignment.Bottom) > 0) absolutePosition.Y += actualAnchorTarget.Size.Y;
                else if ((anchorTo & BoxAlignment.Top) == 0) absolutePosition.Y += actualAnchorTarget.Size.Y * .5f;
            }
            else absolutePosition = offset;

            absolutePosition = manager.SnapToPixel(absolutePosition);
        }

        if (!includeChildren) return;
        foreach (var child in children) child.UpdateAnchoring(iteration);
    }

    #endregion

    #region Layout

    public virtual Vector2 MinSize => PreferredSize;
    public virtual Vector2 MaxSize => Vector2.Zero;
    public virtual Vector2 PreferredSize => DefaultSize;

    public Vector2 DefaultSize = Vector2.Zero;

    bool canGrow = true;

    public bool CanGrow
    {
        get => canGrow;
        set
        {
            if (canGrow == value) return;
            canGrow = value;
            InvalidateAncestorLayout();
        }
    }

    public bool NeedsLayout { get; private set; } = true;

    public void Pack(float width = 0, float height = 0, float maxWidth = 0, float maxHeight = 0)
    {
        while (true)
        {
            var preferredSize = PreferredSize;

            var newSize = preferredSize;
            if (width > 0 && (maxWidth == 0 || maxWidth > 0 && newSize.X < width)) newSize.X = width;
            if (height > 0 && (maxHeight == 0 || maxHeight > 0 && newSize.Y < height)) newSize.Y = height;
            if (maxWidth > 0 && newSize.X > maxWidth) newSize.X = maxWidth;
            if (maxHeight > 0 && newSize.Y > maxHeight) newSize.Y = maxHeight;
            Size = newSize;

            // Flow layouts and labels don't know their height until they know their width
            manager.RefreshAnchors();
            if (preferredSize != PreferredSize) continue;
            break;
        }
    }
    public void InvalidateAncestorLayout()
    {
        InvalidateLayout();
        Parent?.InvalidateAncestorLayout();
    }
    public virtual void InvalidateLayout()
    {
        NeedsLayout = true;
        manager.InvalidateAnchors();
    }
    public void ValidateLayout()
    {
        if (!NeedsLayout) return;
        Layout();
    }
    public virtual void PreLayout()
    {
        foreach (var child in children) child.PreLayout();
    }
    protected virtual void Layout() => NeedsLayout = false;

    #endregion

    #region Events

    public delegate void WidgetEventHandler<in TEventArgs>(WidgetEvent evt, TEventArgs e);
    public delegate bool HandleableWidgetEventHandler<in TEventArgs>(WidgetEvent evt, TEventArgs e);

    public event HandleableWidgetEventHandler<MouseButtonEventArgs> OnClickDown;
    public bool NotifyClickDown(WidgetEvent evt, MouseButtonEventArgs e) => Raise(OnClickDown, evt, e);

    public event WidgetEventHandler<MouseButtonEventArgs> OnClickUp;
    public bool NotifyClickUp(WidgetEvent evt, MouseButtonEventArgs e)
    {
        Raise(OnClickUp, evt, e);
        return false;
    }

    public event WidgetEventHandler<MouseMoveEventArgs> OnClickMove;
    public bool NotifyClickMove(WidgetEvent evt, MouseMoveEventArgs e)
    {
        Raise(OnClickMove, evt, e);
        return false;
    }

    public event HandleableWidgetEventHandler<MouseWheelEventArgs> OnMouseWheel;
    public bool NotifyMouseWheel(WidgetEvent evt, MouseWheelEventArgs e) => Raise(OnMouseWheel, evt, e);

    public event HandleableWidgetEventHandler<KeyboardKeyEventArgs> OnKeyDown;
    public bool NotifyKeyDown(WidgetEvent evt, KeyboardKeyEventArgs e) => Raise(OnKeyDown, evt, e);

    public event HandleableWidgetEventHandler<KeyboardKeyEventArgs> OnKeyUp;
    public bool NotifyKeyUp(WidgetEvent evt, KeyboardKeyEventArgs e) => Raise(OnKeyUp, evt, e);

    public event HandleableWidgetEventHandler<TextInputEventArgs> OnKeyPress;
    public bool NotifyKeyPress(WidgetEvent evt, TextInputEventArgs e) => Raise(OnKeyPress, evt, e);

    public event WidgetEventHandler<WidgetHoveredEventArgs> OnHovered;
    public bool NotifyHoveredWidgetChange(WidgetEvent evt, WidgetHoveredEventArgs e)
    {
        var related = evt.RelatedTarget;
        while (related is not null && related != this) related = related.Parent;

        if (related != this) Raise(OnHovered, evt, e);
        return false;
    }

    public event WidgetEventHandler<WidgetFocusEventArgs> OnFocusChange;
    public bool NotifyFocusChange(WidgetEvent evt, WidgetFocusEventArgs e)
    {
        Raise(OnFocusChange, evt, e);
        return false;
    }

    protected static bool Raise<T>(HandleableWidgetEventHandler<T> handler, WidgetEvent evt, T e)
    {
        if (handler is null) return evt.Handled;
        var invocationList = handler.GetInvocationList();

        foreach (var handlerDelegate in invocationList)
            try
            {
                if (!invocationList.Contains(handlerDelegate) ||
                    !((HandleableWidgetEventHandler<T>)handlerDelegate)(evt, e)) continue;

                evt.Handled = true;
                break;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Event handler '{handler.Method}' for '{handler.Target}':\n{ex}");
            }

        return evt.Handled;
    }

    static void Raise<T>(WidgetEventHandler<T> handler, WidgetEvent evt, T e)
        => EventHelper.InvokeStrict(handler, d => ((WidgetEventHandler<T>)d)(evt, e));

    public event EventHandler OnDisposed;

    #endregion

    #region Drag and Drop

    public Func<object> GetDragData;
    public Func<object, bool> HandleDrop;

    #endregion

    #region IDisposable Support

    public bool IsDisposed { get; private set; }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (disposing)
        {
            Parent?.Remove(this);
            manager.NotifyWidgetDisposed(this);
            ClearWidgets();
        }

        children.Clear();
        Tooltip = null;

        if (disposing) OnDisposed?.Invoke(this, EventArgs.Empty);
        IsDisposed = true;
    }

    public void Dispose() => Dispose(true);

    #endregion
}