namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Drawables;
using BrewLib.Input;
using BrewLib.ScreenLayers;
using BrewLib.UserInterface.Skinning;
using BrewLib.Util;
using SDL3;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.Temporary;

public sealed class WidgetManager : IInputHandler, IDisposable
{
    readonly PooledDictionary<byte, Widget> clickTargets = new();

    public readonly InputManager InputManager;
    public readonly Widget Root;
    readonly StackLayout rootContainer;
    public readonly ScreenLayerManager ScreenLayerManager;
    public readonly Skin Skin;

    readonly Widget tooltipOverlay;
    ICamera camera;
    public Widget HoveredWidget;
    Widget keyboardFocus;
    Vector2 mousePosition;

    public WidgetManager(ScreenLayerManager screenLayerManager, InputManager inputManager, Skin skin)
    {
        ScreenLayerManager = screenLayerManager;
        InputManager = inputManager;
        Skin = skin;

        rootContainer = new(this) { FitChildren = true };

        rootContainer.Add(Root = new StackLayout(this) { FitChildren = true });

        rootContainer.Add(tooltipOverlay = new(this) { Hoverable = false });
        dragDrawable = Skin.GetDrawable("dragCursor");
    }

    public Vector2 Size { get => rootContainer.Size; set => rootContainer.Size = value; }

    public float Opacity { get => rootContainer.Opacity; set => rootContainer.Opacity = value; }

    public Widget KeyboardFocus
    {
        get => keyboardFocus;
        set
        {
            if (keyboardFocus == value) return;

            if (keyboardFocus is not null)
                fire((w, evt, _) => w.NotifyFocusChange(evt, new(false)), keyboardFocus, value, 0);

            var previousFocus = keyboardFocus;
            keyboardFocus = value;

            if (keyboardFocus is not null)
            {
                SDL.StartTextInput(InputManager.Window);

                fire((w, evt, _) => w.NotifyFocusChange(evt, new(true)), keyboardFocus, previousFocus, 0);
            }
            else SDL.StopTextInput(InputManager.Window);
        }
    }

    public Vector2 MousePosition => mousePosition;

    public ICamera Camera
    {
        get => camera;
        set
        {
            if (camera == value) return;

            if (camera is not null) camera.Changed -= CameraChanged;
            camera = value;
            if (camera is not null) camera.Changed += CameraChanged;
            RefreshHover();
        }
    }

    void CameraChanged(ICamera c) => InvalidateAnchors();

    public void RefreshHover()
    {
        if (camera is not null && InputManager.HasMouseFocus)
        {
            var fromScreen = camera.FromScreen(InputManager.MousePosition);
            mousePosition = new(fromScreen.X, fromScreen.Y);
            changeHoveredWidget(rootContainer.GetWidgetAt(fromScreen.X, fromScreen.Y));

            hoveredDraggableWidget = HoveredWidget;
            while (hoveredDraggableWidget is not null && hoveredDraggableWidget.GetDragData is null)
                hoveredDraggableWidget = hoveredDraggableWidget.Parent;
        }
        else changeHoveredWidget(null);
    }

    public void NotifyWidgetDisposed(Widget widget)
    {
        if (HoveredWidget == widget) RefreshHover();
        if (keyboardFocus == widget) keyboardFocus = null;

        DisableGamepadEvents(widget);

        using var buttons = TempArray.Create(clickTargets.AsReadOnlySpan());
        foreach (var key in buttons)
            if (key.Value == widget)
                clickTargets.Remove(key.Key);
    }

    public void Draw(DrawContext drawContext)
    {
        if (rootContainer.Visible) rootContainer.Draw(drawContext, 1);
        if (!IsDragging) return;

        dragDrawable.Draw(drawContext,
            camera,
            new(mousePosition.X + dragOffset.X, mousePosition.Y + dragOffset.Y, dragSize.X, dragSize.Y));
    }

    #region Tooltip

    readonly PooledDictionary<Widget, Widget> tooltips = new();

    public void RegisterTooltip(Widget widget, ReadOnlySpan<char> text)
    {
        if (tooltips.TryGetValue(widget, out var tooltip) && tooltip is Label label)
        {
            label.Text = text;
            if (label.NeedsLayout)
            {
                label.Pack(650);
                label.Pack();
            }

            return;
        }

        RegisterTooltip(widget, new Label(this) { StyleName = "tooltip", AnchorTarget = widget, Text = text });
    }

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
        var topSpace = targetBounds.Y - rootBounds.Y;

        tooltip.Offset = Vector2.Zero;
        tooltip.AnchorFrom = BoxAlignment.Bottom;
        tooltip.AnchorTo = BoxAlignment.Top;
        tooltip.Pack(0, 0, 600, topSpace - 16);

        // Only put it on the bottom if it doesn't fit on top

        var bounds = tooltip.Bounds;
        if (bounds.Y < rootBounds.Y + 16)
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
        refreshingAnchors = false;
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

    readonly Drawable dragDrawable;
    Vector2 dragOffset, dragSize;
    Widget hoveredDraggableWidget;
    readonly PooledDictionary<byte, object> dragData = [];

    public bool IsDragging
    {
        get
        {
            foreach (var value in dragData.Values)
                if (value is not null)
                    return true;

            return false;
        }
    }

    void startDragAndDrop(byte button)
    {
        if (hoveredDraggableWidget is null || dragData.TryGetValue(button, out var data) && data is not null) return;

        dragOffset = hoveredDraggableWidget.AbsolutePosition - mousePosition;
        dragSize = hoveredDraggableWidget.Size;
        dragData[button] = hoveredDraggableWidget.GetDragData();
    }

    void endDragAndDrop(byte button)
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

    #endregion

    #region Input events

    readonly PooledList<Widget> gamepadTargets = new();

    public void DisableGamepadEvents(Widget widget) => gamepadTargets.Remove(widget);

    public void OnClose(SDL.QuitEvent e) { }

    public void OnResize(WindowEvent e) { }

    public void OnFocusChanged(WindowEvent e) => RefreshHover();

    public bool OnClickDown(SDL.MouseButtonEvent e)
    {
        var target = HoveredWidget ?? rootContainer;
        if (keyboardFocus is not null && target != keyboardFocus && !target.HasAncestor(keyboardFocus))
            KeyboardFocus = null;

        var widgetEvent = fire((w, evt, ev) => w.NotifyClickDown(evt, ev), target, state: e);
        if (widgetEvent.Handled) clickTargets[e.Button] = widgetEvent.Listener;

        return widgetEvent.Handled;
    }

    public bool OnClickUp(SDL.MouseButtonEvent e)
    {
        endDragAndDrop(e.Button);
        if (clickTargets.TryGetValue(e.Button, out var clickTarget)) clickTargets[e.Button] = null;

        var target = clickTarget ?? HoveredWidget ?? rootContainer;
        return fire((w, evt, ev) => w.NotifyClickUp(evt, ev), target, HoveredWidget ?? rootContainer, e).Handled;
    }

    public void OnMouseMove(SDL.MouseMotionEvent e)
    {
        RefreshHover();
        foreach (var (key, clickTarget) in clickTargets)
        {
            if (clickTarget is null) continue;

            startDragAndDrop(key);
            fire((w, evt, ev) => w.NotifyClickMove(evt, ev), clickTarget, HoveredWidget, e);
        }
    }

    public bool OnMouseWheel(SDL.MouseWheelEvent e)
        => fire((w, evt, ev) => w.NotifyMouseWheel(evt, ev), HoveredWidget ?? rootContainer, state: e).Handled;

    public bool OnKeyDown(KeyboardEvent e)
        => fire((w, evt, ev) => w.NotifyKeyDown(evt, ev), keyboardFocus ?? HoveredWidget ?? rootContainer, state: e)
            .Handled;

    public bool OnKeyUp(KeyboardEvent e)
        => fire((w, evt, ev) => w.NotifyKeyUp(evt, ev), keyboardFocus ?? HoveredWidget ?? rootContainer, state: e)
            .Handled;

    public bool OnKeyPress(TextInputEvent e)
        => fire((w, evt, ev) => w.NotifyKeyPress(evt, ev), keyboardFocus ?? HoveredWidget ?? rootContainer, state: e)
            .Handled;

    void changeHoveredWidget(Widget widget)
    {
        if (widget == HoveredWidget) return;

        if (HoveredWidget is not null && !HoveredWidget.IsDisposed)
            fire((w, evt, _) => w.NotifyHoveredWidgetChange(evt, new(false)), HoveredWidget, widget, 0);

        var previousWidget = HoveredWidget;
        HoveredWidget = widget;

        if (HoveredWidget is not null && !HoveredWidget.IsDisposed)
            fire((w, evt, _) => w.NotifyHoveredWidgetChange(evt, new(true)), HoveredWidget, previousWidget, 0);
    }

    static WidgetEvent fire<TState>(Func<Widget, WidgetEvent, TState, bool> notify,
        Widget target,
        Widget relatedTarget = null,
        TState state = default,
        bool bubbles = true)
    {
        ObjectDisposedException.ThrowIf(target.IsDisposed, target);

        WidgetEvent widgetEvent = new(relatedTarget) { Listener = target };
        if (notify(target, widgetEvent, state) || !bubbles) return widgetEvent;

        for (var ancestor = target.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            widgetEvent.Listener = ancestor;
            if (notify(ancestor, widgetEvent, state)) return widgetEvent;
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
        if (camera is not null) camera.Changed -= CameraChanged;

        clickTargets.Dispose();
        dragData.Dispose();
        gamepadTargets.Dispose();

        if (disposing) disposed = true;
    }

    #endregion
}