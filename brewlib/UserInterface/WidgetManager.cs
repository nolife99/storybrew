namespace BrewLib.UserInterface;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Graphics;
using Graphics.Cameras;
using Graphics.Drawables;
using Input;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ScreenLayers;
using Skinning;
using Util;

public sealed class WidgetManager : InputHandler, IDisposable
{
    readonly Dictionary<MouseButton, Widget> clickTargets = [];

    public readonly InputManager InputManager;
    public readonly Widget Root;
    public readonly ScreenLayerManager ScreenLayerManager;
    public readonly Skin Skin;

    readonly Widget tooltipOverlay;
    Camera camera;
    public Widget HoveredWidget;
    Widget keyboardFocus;
    Vector2 mousePosition;
    StackLayout rootContainer;

    public WidgetManager(ScreenLayerManager screenLayerManager, InputManager inputManager, Skin skin)
    {
        ScreenLayerManager = screenLayerManager;
        InputManager = inputManager;
        Skin = skin;

        rootContainer = new(this) { FitChildren = true };

        rootContainer.Add(Root = new StackLayout(this) { FitChildren = true });

        rootContainer.Add(tooltipOverlay = new(this) { Hoverable = false });

        initializeDragAndDrop();
    }

    public Vector2 Size { get => rootContainer.Size; set => rootContainer.Size = value; }

    public float Opacity { get => rootContainer.Opacity; set => rootContainer.Opacity = value; }

    public Widget KeyboardFocus
    {
        get => keyboardFocus;
        set
        {
            if (keyboardFocus == value) return;

            if (keyboardFocus is not null) fire((w, evt) => w.NotifyFocusChange(evt, new(false)), keyboardFocus, value);
            var previousFocus = keyboardFocus;
            keyboardFocus = value;

            if (keyboardFocus is not null) fire((w, evt) => w.NotifyFocusChange(evt, new(true)), keyboardFocus, previousFocus);
        }
    }

    public Vector2 MousePosition => mousePosition;

    public Camera Camera
    {
        get => camera;
        set
        {
            if (camera == value) return;
            if (camera is not null) camera.Changed -= camera_Changed;
            camera = value;
            if (camera is not null) camera.Changed += camera_Changed;
            RefreshHover();
        }
    }

    public void RefreshHover()
    {
        if (camera is not null && InputManager.HasMouseFocus)
        {
            var fromScreen = camera.FromScreen(InputManager.MousePosition);
            mousePosition = new(fromScreen.X, fromScreen.Y);
            changeHoveredWidget(rootContainer.GetWidgetAt(fromScreen.X, fromScreen.Y));
            updateHoveredDraggable();
        }
        else changeHoveredWidget(null);
    }
    public void NotifyWidgetDisposed(Widget widget)
    {
        if (HoveredWidget == widget) RefreshHover();
        if (keyboardFocus == widget) keyboardFocus = null;

        DisableGamepadEvents(widget);

        foreach (var key in clickTargets.Keys.Where(key => clickTargets[key] == widget)) clickTargets.Remove(key);
    }
    public void Draw(DrawContext drawContext)
    {
        if (rootContainer.Visible) rootContainer.Draw(drawContext, 1);
        drawDragIndicator(drawContext);
    }
    void camera_Changed(object sender, EventArgs e) => InvalidateAnchors();

    #region Tooltip

    readonly Dictionary<Widget, Widget> tooltips = [];

    public void RegisterTooltip(Widget widget, string text) => RegisterTooltip(widget,
        new Label(this) { StyleName = "tooltip", AnchorTarget = widget, Text = text });

    public void RegisterTooltip(Widget widget, Widget tooltip)
    {
        UnregisterTooltip(widget);

        tooltip.Displayed = false;

        tooltips[widget] = tooltip;
        tooltipOverlay.Add(tooltip);
        widget.OnHovered += TooltipWidget_OnHovered;

        if (widget == HoveredWidget) displayTooltip(tooltip);
    }
    public void UnregisterTooltip(Widget widget)
    {
        if (!tooltips.Remove(widget, out var tooltip)) return;
        tooltip.Dispose();
        widget.OnHovered -= TooltipWidget_OnHovered;
    }
    void TooltipWidget_OnHovered(WidgetEvent evt, WidgetHoveredEventArgs e)
    {
        var tooltip = tooltips[evt.Listener];
        if (e.Hovered) displayTooltip(tooltip);
        else tooltip.Displayed = false;
    }
    void displayTooltip(Widget tooltip)
    {
        var rootBounds = rootContainer.Bounds;
        var targetBounds = tooltip.AnchorTarget.Bounds;
        var topSpace = targetBounds.Top - rootBounds.Top;

        tooltip.Offset = Vector2.Zero;
        tooltip.AnchorFrom = BoxAlignment.Bottom;
        tooltip.AnchorTo = BoxAlignment.Top;
        tooltip.Pack(0, 0, 600, topSpace - 16);

        // Only put it on the bottom if it doesn't fit on top

        var bounds = tooltip.Bounds;
        if (bounds.Top < rootBounds.Top + 16)
        {
            var bottomSpace = rootBounds.Bottom - targetBounds.Bottom;
            if (bottomSpace > topSpace)
            {
                tooltip.AnchorFrom = BoxAlignment.Top;
                tooltip.AnchorTo = BoxAlignment.Bottom;
                tooltip.Pack(0, 0, 600, bottomSpace - 16);
                bounds = tooltip.Bounds;
            }
        }

        var offsetX = 0f;
        if (bounds.Right > rootBounds.Right - 16) offsetX = rootBounds.Right - 16 - bounds.Right;
        else if (bounds.Left < rootBounds.Left + 16) offsetX = rootBounds.Left + 16 - bounds.Left;

        tooltip.Offset = new(offsetX, 0);
        tooltip.Displayed = true;
    }

    #endregion

    #region Placement

    bool needsAnchorUpdate, refreshingAnchors;
    int anchoringIteration;

    public void InvalidateAnchors()
    {
        needsAnchorUpdate = true;
        if (!keyboardFocus?.Visible ?? false) KeyboardFocus = null;
    }
    public void RefreshAnchors()
    {
        if (!needsAnchorUpdate || refreshingAnchors) return;
        try
        {
            refreshingAnchors = true;
            var iterationBefore = anchoringIteration;

            rootContainer.PreLayout();
            while (needsAnchorUpdate)
            {
                needsAnchorUpdate = false;
                if (anchoringIteration - iterationBefore > 8) break;

                rootContainer.UpdateAnchoring(++anchoringIteration);
            }

            RefreshHover();
        }
        finally
        {
            refreshingAnchors = false;
        }
    }

    public float PixelSize => 1 / ((camera as CameraOrtho)?.HeightScaling ?? 1);
    public float SnapToPixel(float value)
    {
        var scaling = (camera as CameraOrtho)?.HeightScaling ?? 1;
        return float.Round(value * scaling) / scaling;
    }
    public Vector2 SnapToPixel(Vector2 value)
    {
        var scaling = (camera as CameraOrtho)?.HeightScaling ?? 1;
        return new(float.Round(value.X * scaling) / scaling, float.Round(value.Y * scaling) / scaling);
    }

    #endregion

    #region Drag and Drop

    Drawable dragDrawable;
    Vector2 dragOffset, dragSize;
    Widget hoveredDraggableWidget;
    readonly Dictionary<MouseButton, object> dragData = [];

    public bool IsDragging => dragData.Values.Any(v => v is not null);

    void startDragAndDrop(MouseButton button)
    {
        if (hoveredDraggableWidget is null || dragData.TryGetValue(button, out var data) && data is not null) return;

        dragOffset = hoveredDraggableWidget.AbsolutePosition - mousePosition;
        dragSize = hoveredDraggableWidget.Size;
        dragData[button] = hoveredDraggableWidget.GetDragData();
    }
    void endDragAndDrop(MouseButton button)
    {
        if (!dragData.TryGetValue(button, out var data) || data is null) return;
        dragData[button] = null;

        var dropTarget = HoveredWidget ?? rootContainer;
        while (dropTarget is not null)
        {
            if (dropTarget.HandleDrop is not null && dropTarget.HandleDrop(data)) break;
            dropTarget = dropTarget.Parent;
        }
    }
    void initializeDragAndDrop() => dragDrawable = Skin.GetDrawable("dragCursor");

    void updateHoveredDraggable()
    {
        hoveredDraggableWidget = HoveredWidget;
        while (hoveredDraggableWidget is not null && hoveredDraggableWidget.GetDragData is null)
            hoveredDraggableWidget = hoveredDraggableWidget.Parent;
    }
    void drawDragIndicator(DrawContext drawContext)
    {
        if (!IsDragging) return;
        dragDrawable.Draw(drawContext, Camera,
            new(mousePosition.X + dragOffset.X, mousePosition.Y + dragOffset.Y, dragSize.X, dragSize.Y));
    }

    #endregion

    #region Input events

    readonly List<Widget> gamepadTargets = [];

    public void DisableGamepadEvents(Widget widget) => gamepadTargets.Remove(widget);

    public void OnFocusChanged(FocusedChangedEventArgs e) => RefreshHover();
    public bool OnClickDown(MouseButtonEventArgs e)
    {
        var target = HoveredWidget ?? rootContainer;
        if (keyboardFocus is not null && target != keyboardFocus && !target.HasAncestor(keyboardFocus)) KeyboardFocus = null;

        var widgetEvent = fire((w, evt) => w.NotifyClickDown(evt, e), target);
        if (widgetEvent.Handled) clickTargets[e.Button] = widgetEvent.Listener;

        return widgetEvent.Handled;
    }
    public bool OnClickUp(MouseButtonEventArgs e)
    {
        endDragAndDrop(e.Button);
        if (clickTargets.TryGetValue(e.Button, out var clickTarget)) clickTargets[e.Button] = null;

        var target = clickTarget ?? HoveredWidget ?? rootContainer;
        return fire((w, evt) => w.NotifyClickUp(evt, e), target, HoveredWidget ?? rootContainer).Handled;
    }
    public void OnMouseMove(MouseMoveEventArgs e)
    {
        RefreshHover();
        foreach (var (key, clickTarget) in clickTargets)
        {
            if (clickTarget is null) continue;

            startDragAndDrop(key);
            fire((w, evt) => w.NotifyClickMove(evt, e), clickTarget, HoveredWidget);
        }
    }
    public bool OnMouseWheel(MouseWheelEventArgs e)
        => fire((w, evt) => w.NotifyMouseWheel(evt, e), HoveredWidget ?? rootContainer).Handled;

    public bool OnKeyDown(KeyboardKeyEventArgs e)
        => fire((w, evt) => w.NotifyKeyDown(evt, e), keyboardFocus ?? HoveredWidget ?? rootContainer).Handled;

    public bool OnKeyUp(KeyboardKeyEventArgs e)
        => fire((w, evt) => w.NotifyKeyUp(evt, e), keyboardFocus ?? HoveredWidget ?? rootContainer).Handled;

    public bool OnKeyPress(TextInputEventArgs e)
        => fire((w, evt) => w.NotifyKeyPress(evt, e), keyboardFocus ?? HoveredWidget ?? rootContainer).Handled;

    void changeHoveredWidget(Widget widget)
    {
        if (widget == HoveredWidget) return;
        if (HoveredWidget is not null) fire((w, evt) => w.NotifyHoveredWidgetChange(evt, new(false)), HoveredWidget, widget);

        var previousWidget = HoveredWidget;
        HoveredWidget = widget;

        if (HoveredWidget is not null)
            fire((w, evt) => w.NotifyHoveredWidgetChange(evt, new(true)), HoveredWidget, previousWidget);
    }

    static WidgetEvent fire(Func<Widget, WidgetEvent, bool> notify,
        List<Widget> targets,
        Widget relatedTarget = null,
        bool bubbles = true)
    {
        foreach (var widgetEvent in targets.Select(t => fire(notify, t, relatedTarget, bubbles))
            .Where(widgetEvent => widgetEvent.Handled)) return widgetEvent;

        return new(relatedTarget);
    }
    static WidgetEvent fire(Func<Widget, WidgetEvent, bool> notify,
        Widget target,
        Widget relatedTarget = null,
        bool bubbles = true)
    {
        ObjectDisposedException.ThrowIf(target.IsDisposed, nameof(target));

        WidgetEvent widgetEvent = new(relatedTarget);
        var ancestors = bubbles ? target.GetAncestors() : null;

        widgetEvent.Listener = target;
        if (notify(target, widgetEvent)) return widgetEvent;

        foreach (var ancestor in ancestors)
        {
            widgetEvent.Listener = ancestor;
            if (notify(ancestor, widgetEvent)) return widgetEvent;
        }

        return widgetEvent;
    }

    #endregion

    #region IDisposable Support

    bool disposed;
    public void Dispose() => Dispose(true);
    void Dispose(bool disposing)
    {
        if (disposed) return;
        rootContainer.Dispose();
        if (camera is not null) camera.Changed -= camera_Changed;

        if (disposing) disposed = true;
    }

    #endregion
}