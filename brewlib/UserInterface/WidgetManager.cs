using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Drawables;
using BrewLib.Input;
using BrewLib.ScreenLayers;
using BrewLib.UserInterface.Skinning;
using BrewLib.Util;
using osuTK.Input;

namespace BrewLib.UserInterface;

public sealed class WidgetManager : InputHandler, IDisposable
{
    public readonly InputManager InputManager;
    public readonly ScreenLayerManager ScreenLayerManager;
    public readonly Skin Skin;

    public readonly Widget Root;

    StackLayout rootContainer;
    readonly Widget tooltipOverlay;
    readonly Dictionary<MouseButton, Widget> clickTargets = [];
    readonly Dictionary<GamepadButton, Widget> gamepadButtonTargets = [];

    public Vector2 Size
    {
        get => rootContainer.Size;
        set => rootContainer.Size = value;
    }
    public float Opacity
    {
        get => rootContainer.Opacity;
        set => rootContainer.Opacity = value;
    }
    public Widget HoveredWidget;

    Widget keyboardFocus;
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

    Vector2 mousePosition;
    public Vector2 MousePosition => mousePosition;

    Camera camera;
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
        
        foreach (var key in clickTargets.Keys) if (clickTargets[key] == widget) clickTargets.Remove(key);
        foreach (var key in gamepadButtonTargets.Keys) if (gamepadButtonTargets[key] == widget) gamepadButtonTargets.Remove(key);
    }
    public void Draw(DrawContext drawContext)
    {
        if (rootContainer.Visible) rootContainer.Draw(drawContext, 1);
        drawDragIndicator(drawContext);
    }
    void camera_Changed(object sender, EventArgs e) => InvalidateAnchors();

    #region Tooltip

    readonly Dictionary<Widget, Widget> tooltips = [];

    public void RegisterTooltip(Widget widget, string text) => RegisterTooltip(widget, new Label(this)
    {
        StyleName = "tooltip",
        AnchorTarget = widget,
        Text = text
    });
    public void RegisterTooltip(Widget widget, Widget tooltip)
    {
        UnregisterTooltip(widget);

        tooltip.Displayed = false;

        tooltips.Add(widget, tooltip);
        tooltipOverlay.Add(tooltip);
        widget.OnHovered += TooltipWidget_OnHovered;

        if (widget == HoveredWidget) displayTooltip(tooltip);
    }
    public void UnregisterTooltip(Widget widget)
    {
        if (tooltips.TryGetValue(widget, out var tooltip))
        {
            tooltips.Remove(widget);
            tooltip.Dispose();
            widget.OnHovered -= TooltipWidget_OnHovered;
        }
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
                if (anchoringIteration - iterationBefore > 8)
                {
                    Trace.TraceWarning("Could not resolve ui layout");
                    break;
                }
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

    public double SnapToPixel(double value)
    {
        var scaling = (camera as CameraOrtho)?.HeightScaling ?? 1;
        return Math.Round(value * scaling) / scaling;
    }
    public Vector2 SnapToPixel(Vector2 value)
    {
        var scaling = (camera as CameraOrtho)?.HeightScaling ?? 1;
        return new(MathF.Round(value.X * scaling) / scaling, MathF.Round(value.Y * scaling) / scaling);
    }

    #endregion

    #region Drag and Drop

    Drawable dragDrawable;
    Vector2 dragOffset, dragSize;
    Widget hoveredDraggableWidget;
    readonly Dictionary<MouseButton, object> dragData = [];

    public bool CanDrag => hoveredDraggableWidget is not null;
    public bool IsDragging => dragData.Values.Any(v => v is not null);

    void startDragAndDrop(MouseButton button)
    {
        if (hoveredDraggableWidget is null || (dragData.TryGetValue(button, out var data) && data is not null)) return;

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
        while (hoveredDraggableWidget is not null && hoveredDraggableWidget.GetDragData is null) hoveredDraggableWidget = hoveredDraggableWidget.Parent;
    }
    void drawDragIndicator(DrawContext drawContext)
    {
        if (!IsDragging) return;
        dragDrawable.Draw(drawContext, Camera, new(mousePosition.X + dragOffset.X, mousePosition.Y + dragOffset.Y, dragSize.X, dragSize.Y));
    }

    #endregion

    #region Input events

    readonly List<Widget> gamepadTargets = [];

    public void EnableGamepadEvents(Widget widget) => gamepadTargets.Insert(0, widget);
    public void DisableGamepadEvents(Widget widget) => gamepadTargets.Remove(widget);

    public void OnFocusChanged(FocusChangedEventArgs e) => RefreshHover();
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
        if (clickTargets.TryGetValue(e.Button, out Widget clickTarget)) clickTargets[e.Button] = null;

        var target = clickTarget ?? HoveredWidget ?? rootContainer;
        return fire((w, evt) => w.NotifyClickUp(evt, e), target, relatedTarget: HoveredWidget ?? rootContainer).Handled;
    }
    public void OnMouseMove(MouseMoveEventArgs e)
    {
        RefreshHover();
        foreach (var entry in clickTargets)
        {
            var clickTarget = entry.Value;
            if (clickTarget is null) continue;

            startDragAndDrop(entry.Key);
            fire((w, evt) => w.NotifyClickMove(evt, e), clickTarget, relatedTarget: HoveredWidget);
        }
    }
    public bool OnMouseWheel(MouseWheelEventArgs e) => fire((w, evt) => w.NotifyMouseWheel(evt, e), HoveredWidget ?? rootContainer).Handled;
    public bool OnKeyDown(KeyboardKeyEventArgs e) => fire((w, evt) => w.NotifyKeyDown(evt, e), keyboardFocus ?? HoveredWidget ?? rootContainer).Handled;
    public bool OnKeyUp(KeyboardKeyEventArgs e) => fire((w, evt) => w.NotifyKeyUp(evt, e), keyboardFocus ?? HoveredWidget ?? rootContainer).Handled;
    public bool OnKeyPress(osuTK.KeyPressEventArgs e) => fire((w, evt) => w.NotifyKeyPress(evt, e), keyboardFocus ?? HoveredWidget ?? rootContainer).Handled;

    void changeHoveredWidget(Widget widget)
    {
        if (widget == HoveredWidget) return;
        if (HoveredWidget is not null) fire((w, evt) => w.NotifyHoveredWidgetChange(evt, new(false)), HoveredWidget, widget);

        var previousWidget = HoveredWidget;
        HoveredWidget = widget;

        if (HoveredWidget is not null) fire((w, evt) => w.NotifyHoveredWidgetChange(evt, new(true)), HoveredWidget, previousWidget);
    }

    public void OnGamepadConnected(GamepadEventArgs e) { }
    public bool OnGamepadButtonDown(GamepadButtonEventArgs e)
    {
        var widgetEvent = fire((w, evt) => w.NotifyGamepadButtonDown(evt, e), gamepadTargets, bubbles: false);
        if (widgetEvent.Handled) gamepadButtonTargets[e.Button] = widgetEvent.Listener;

        return widgetEvent.Handled;
    }
    public bool OnGamepadButtonUp(GamepadButtonEventArgs e)
    {
        if (gamepadButtonTargets.TryGetValue(e.Button, out Widget buttonTarget))
        {
            gamepadButtonTargets[e.Button] = null;
            return fire((w, evt) => w.NotifyGamepadButtonUp(evt, e), buttonTarget, bubbles: false).Handled;
        }
        return false;
    }
    static WidgetEvent fire(Func<Widget, WidgetEvent, bool> notify, List<Widget> targets, Widget relatedTarget = null, bool bubbles = true)
    {
        for (var i = 0; i < targets.Count; ++i)
        {
            var widgetEvent = fire(notify, targets[i], relatedTarget, bubbles);
            if (widgetEvent.Handled) return widgetEvent;
        }
        return new(targets.Count > 0 ? targets[^1] : null, relatedTarget);
    }
    static WidgetEvent fire(Func<Widget, WidgetEvent, bool> notify, Widget target, Widget relatedTarget = null, bool bubbles = true)
    {
        ObjectDisposedException.ThrowIf(target.IsDisposed, nameof(target));

        WidgetEvent widgetEvent = new(target, relatedTarget);
        var ancestors = bubbles ? target.GetAncestors() : null;

        widgetEvent.Listener = target;
        if (notify(target, widgetEvent)) return widgetEvent;

        if (!ancestors.IsEmpty) for (var i = 0; i < ancestors.Length; ++i)
        {
            widgetEvent.Listener = ancestors[i];
            if (notify(ancestors[i], widgetEvent)) return widgetEvent;
        }

        return widgetEvent;
    }

    #endregion

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            rootContainer.Dispose();
            if (camera is not null) camera.Changed -= camera_Changed;

            if (disposing)
            {
                camera = null;
                rootContainer = null;

                disposed = true;
            }
        }
    }

    #endregion
}