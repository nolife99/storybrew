namespace BrewLib.ScreenLayers;

using System;
using System.Runtime.CompilerServices;
using BrewLib.Graphics;
using BrewLib.Input;
using BrewLib.Time;
using SDL3;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.Temporary;

public sealed class ScreenLayerManager : InputAdapter, IDisposable
{
    readonly object context;

    readonly InputDispatcher inputDispatcher = new();

    readonly PooledList<ScreenLayer> layers = [], removedLayers = [], updateQueue = [];

    public readonly nint Window;
    ScreenLayer focusedLayer;

    public ScreenLayerManager(nint window, FrameTimeSource timeSource, object context)
    {
        Window = window;
        TimeSource = timeSource;
        this.context = context;

        inputDispatcher.Add(this);
    }

    public FrameTimeSource TimeSource { get; }
    public IInputHandler InputHandler => inputDispatcher;
    public T GetContext<T>() where T : class => Unsafe.As<T>(context);

    public void Add(ScreenLayer layer)
    {
        layer.Manager = this;
        layers.Add(layer);

        layer.Load();

        SDL.GetWindowSizeInPixels(Window, out var width, out var height);
        layer.Resize(int.Max(1, width), int.Max(1, height));
    }

    public void Set(ScreenLayer layer)
    {
        for (var i = layers.Count - 1; i >= 0; --i) layers[i].Exit();
        Add(layer);
    }

    public void Remove(ScreenLayer layer)
    {
        if (focusedLayer == layer) changeFocus(null);

        layers.Remove(layer);
        removedLayers.Add(layer);
        updateQueue.Remove(layer);
    }

    public bool Close()
    {
        for (var i = layers.Count - 1; i >= 0; --i)
        {
            var layer = layers[i];
            if (layer.IsExiting) continue;

            layer.Close();
            return true;
        }

        return false;
    }

    public void Exit()
    {
        using var snapshot = TempArray.Create(layers.AsReadOnlySpan());
        foreach (var layer in snapshot)
            if (!layer.IsExiting)
                layer.Exit();
    }

    public void Update(bool isFixedRateUpdate)
    {
        var active = (SDL.GetWindowFlags(Window) & WindowFlags.InputFocus) != 0;
        if (!active) changeFocus(null);

        updateQueue.Clear();
        updateQueue.AddRange(layers.AsReadOnlySpan());

        bool covered = false, top = true, hasFocus = active;
        while (updateQueue.Count > 0)
        {
            var layerIndex = updateQueue.Count - 1;
            var layer = updateQueue[layerIndex];
            updateQueue.RemoveAt(layerIndex);

            if (hasFocus)
            {
                if (layer.IsExiting)
                {
                    if (focusedLayer == layer) changeFocus(null);
                }
                else
                {
                    if (focusedLayer != layer) changeFocus(layer);
                    hasFocus = false;
                }
            }

            if (isFixedRateUpdate) layer.FixedUpdate();

            layer.Update(top, covered);

            if (!layer.IsPopup) covered = true;
            top = false;
        }

        if (removedLayers.Count != 0)
        {
            foreach (var layer in removedLayers) layer.Dispose();
            removedLayers.Clear();
        }

        if (layers.Count == 0)
        {
            SDL.HideWindow(Window);

            Event ev = new() { Type = EventType.Quit };
            SDL.PushEvent(ref ev);
        }
    }

    public void Draw(DrawContext drawContext)
    {
        foreach (var layer in layers)
            if (layer.CurrentState is not ScreenLayer.State.Hidden)
                layer.Draw(drawContext);
    }

    void changeFocus(ScreenLayer layer)
    {
        if (focusedLayer is not null)
        {
            inputDispatcher.Remove(focusedLayer.InputHandler);
            focusedLayer.LoseFocus();
            focusedLayer = null;
        }

        if (layer is null) return;

        inputDispatcher.Add(layer.InputHandler);
        layer.GainFocus();
        focusedLayer = layer;
    }

    public override void OnResize(WindowEvent e)
    {
        var width = e.Data1;
        var height = e.Data2;

        if (width == 0 || height == 0) return;

        foreach (var layer in layers) layer.Resize(width, height);
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        changeFocus(null);

        foreach (var layer in layers) layer.Dispose();
        layers.Dispose();

        foreach (var layer in removedLayers) layer.Dispose();
        removedLayers.Dispose();

        updateQueue.Dispose();

        disposed = true;
    }

    #endregion
}