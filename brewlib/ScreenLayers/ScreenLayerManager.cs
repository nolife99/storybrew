﻿using BrewLib.Graphics;
using BrewLib.Input;
using BrewLib.Time;
using BrewLib.Util;
using OpenTK;
using OpenTK.Graphics;
using System;
using System.Collections.Generic;

namespace BrewLib.ScreenLayers
{
    public class ScreenLayerManager : IDisposable
    {
        readonly GameWindow window;
        readonly FrameTimeSource timeSource;
        public FrameTimeSource TimeSource => timeSource;

        readonly object context;
        public T GetContext<T>() => (T)context;

        readonly List<ScreenLayer> layers = new(), removedLayers = new(), updateQueue = new();
        ScreenLayer focusedLayer;

        readonly InputDispatcher inputDispatcher = new();
        public InputHandler InputHandler => inputDispatcher;

        public static Color4 BackgroundColor => Color4.Black;
        public event Action<ScreenLayer> LayerAdded;

        public ScreenLayerManager(GameWindow window, FrameTimeSource timeSource, object context)
        {
            this.window = window;
            this.timeSource = timeSource;
            this.context = context;

            window.Resize += window_Resize;
        }

        public void Add(ScreenLayer layer)
        {
            layer.Manager = this;
            layers.Add(layer);

            LayerAdded?.Invoke(layer);
            layer.Load();

            var width = Math.Max(1, window.Width);
            var height = Math.Max(1, window.Height);
            layer.Resize(width, height);
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
            var snapshot = layers.ToArray();
            for (var i = snapshot.Length - 1; i >= 0; --i)
            {
                var layer = snapshot[i];
                if (layer.IsExiting) continue;

                layer.Exit();
            }
        }
        public void Update(bool isFixedRateUpdate)
        {
            var active = window.Focused;
            if (!active) changeFocus(null);

            updateQueue.Clear();
            for (var i = 0; i < layers.Count; ++i) updateQueue.Add(layers[i]);

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

            if (removedLayers.Count > 0)
            {
                for (var i = 0; i < removedLayers.Count; ++i) removedLayers[i].Dispose();
                removedLayers.Clear();
            }

            if (layers.Count == 0) window.Exit();
        }
        public void Draw(DrawContext drawContext, double tween) => layers.ForEach(layer =>
        {
            var layerTween = Math.Max(layer.MinTween, tween);
            layer.MinTween = layerTween;

            layer.Draw(drawContext, layerTween);
        }, layer => layer.CurrentState != ScreenLayer.State.Hidden);
        void changeFocus(ScreenLayer layer)
        {
            if (focusedLayer != null)
            {
                inputDispatcher.Remove(focusedLayer.InputHandler);
                focusedLayer.LoseFocus();
                focusedLayer = null;
            }
            if (layer != null)
            {
                inputDispatcher.Add(layer.InputHandler);
                layer.GainFocus();
                focusedLayer = layer;
            }
        }
        void window_Resize(object sender, EventArgs e)
        {
            var width = window.Width;
            var height = window.Height;

            if (width == 0 || height == 0) return;

            layers.ForEach(layer => layer.Resize(width, height));
        }

        #region IDisposable Support

        bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    changeFocus(null);
                    for (var i = layers.Count - 1; i >= 0; --i) layers[i].Dispose();
                    window.Resize -= window_Resize;
                }
                disposedValue = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}