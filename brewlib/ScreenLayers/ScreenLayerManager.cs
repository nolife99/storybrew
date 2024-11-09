﻿namespace BrewLib.ScreenLayers;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Graphics;
using Input;
using osuTK;
using Time;
using Util;

public sealed class ScreenLayerManager : IDisposable
{
    readonly object context;

    readonly InputDispatcher inputDispatcher = new();

    readonly List<ScreenLayer> layers = [], removedLayers = [], updateQueue = [];
    readonly GameWindow window;
    ScreenLayer focusedLayer;

    public ScreenLayerManager(GameWindow window, FrameTimeSource timeSource, object context)
    {
        this.window = window;
        TimeSource = timeSource;
        this.context = context;

        window.Resize += window_Resize;
    }

    public FrameTimeSource TimeSource { get; }
    public InputHandler InputHandler => inputDispatcher;
    public T GetContext<T>() where T : class => Unsafe.As<T>(context);

    public event Action<ScreenLayer> LayerAdded;

    public void Add(ScreenLayer layer)
    {
        layer.Manager = this;
        layers.Add(layer);

        LayerAdded?.Invoke(layer);
        layer.Load();
        layer.Resize(Math.Max(1, window.Width), Math.Max(1, window.Height));
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
        foreach (var layer in layers.ToArray())
            if (!layer.IsExiting)
                layer.Exit();
    }
    public void Update(bool isFixedRateUpdate)
    {
        var active = window.Focused;
        if (!active) changeFocus(null);

        updateQueue.Clear();
        layers.ForEach(updateQueue.Add);

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

            if (isFixedRateUpdate)
            {
                layer.FixedUpdate();
                layer.MinTween = 0;
            }

            layer.Update(top, covered);

            if (!layer.IsPopup) covered = true;
            top = false;
        }

        if (removedLayers.Count != 0)
        {
            foreach (var layer in removedLayers) layer.Dispose();
            removedLayers.Clear();
        }

        if (layers.Count == 0) window.Exit();
    }

    public void Draw(DrawContext drawContext, float tween) => layers.ForEach(layer =>
    {
        var layerTween = Math.Max(layer.MinTween, tween);
        layer.MinTween = layerTween;

        layer.Draw(drawContext, layerTween);
    }, layer => layer.CurrentState != ScreenLayer.State.Hidden);

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

    void window_Resize(object sender, EventArgs e)
    {
        var width = window.Width;
        var height = window.Height;

        if (width == 0 || height == 0) return;
        foreach (var layer in layers) layer.Resize(width, height);
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose() => Dispose(true);

    void Dispose(bool disposing)
    {
        if (disposed) return;
        changeFocus(null);
        if (!disposing) return;

        foreach (var layer in layers) layer.Dispose();
        foreach (var layer in removedLayers) layer.Dispose();

        layers.Clear();
        removedLayers.Clear();

        window.Resize -= window_Resize;

        disposed = true;
    }

    #endregion
}