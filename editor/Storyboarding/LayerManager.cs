namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;

public class LayerManager
{
    readonly List<EditorStoryboardLayer> layers = [];

    public int LayersCount => layers.Count;
    public IEnumerable<EditorStoryboardLayer> Layers => layers;

    public IEnumerable<EditorStoryboardLayer> FindLayers(Func<EditorStoryboardLayer, bool> predicate) => layers.Where(predicate);

    public event EventHandler OnLayersChanged;

    public void Add(EditorStoryboardLayer layer)
    {
        layers.Insert(findLayerIndex(layer), layer);
        layer.OnChanged += layer_OnChanged;
        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(List<EditorStoryboardLayer> oldLayers, List<EditorStoryboardLayer> newLayers)
    {
        oldLayers = [..oldLayers];
        foreach (var newLayer in newLayers)
        {
            var oldLayer = oldLayers.Find(l => l.Name == newLayer.Name);
            if (oldLayer is not null)
            {
                var index = layers.IndexOf(oldLayer);
                if (index != -1)
                {
                    newLayer.CopySettings(layers[index]);
                    layers[index] = newLayer;
                }

                oldLayers.Remove(oldLayer);
            }
            else layers.Insert(findLayerIndex(newLayer), newLayer);

            newLayer.OnChanged += layer_OnChanged;
        }

        foreach (var oldLayer in oldLayers)
        {
            oldLayer.OnChanged -= layer_OnChanged;
            layers.Remove(oldLayer);
        }

        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(EditorStoryboardLayer oldLayer, List<EditorStoryboardLayer> newLayers)
    {
        var index = layers.IndexOf(oldLayer);
        if (index != -1)
        {
            foreach (var newLayer in newLayers)
            {
                newLayer.CopySettings(oldLayer);
                newLayer.OnChanged += layer_OnChanged;
            }

            layers.InsertRange(index, newLayers);

            oldLayer.OnChanged -= layer_OnChanged;
            layers.Remove(oldLayer);
        }
        else
            throw new InvalidOperationException(
                $"Cannot replace layer '{oldLayer.Name}' with multiple layers, old layer not found");

        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(EditorStoryboardLayer layer)
    {
        if (!layers.Remove(layer)) return;
        layer.OnChanged -= layer_OnChanged;
        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MoveToOsbLayer(EditorStoryboardLayer layer, OsbLayer osbLayer)
    {
        var firstLayer = layers.FirstOrDefault(l => l.OsbLayer == osbLayer);
        if (firstLayer is not null) MoveToLayer(layer, firstLayer);
        else layer.OsbLayer = osbLayer;
    }

    public void MoveToLayer(EditorStoryboardLayer layerToMove, EditorStoryboardLayer toLayer)
    {
        layerToMove.OsbLayer = toLayer.OsbLayer;

        var fromIndex = layers.IndexOf(layerToMove);
        var toIndex = layers.IndexOf(toLayer);
        if (fromIndex != -1 && toIndex != -1)
        {
            layers.Move(fromIndex, toIndex);
            sortLayer(layerToMove);
        }
        else
            throw new InvalidOperationException($"Cannot move layer '{layerToMove.Name}' to the position of '{
                layerToMove.Name}'");
    }

    public void TriggerEvents(float startTime, float endTime)
    {
        foreach (var layer in layers) layer.TriggerEvents(startTime, endTime);
    }

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, FrameStats frameStats)
    {
        foreach (var layer in layers) layer.Draw(drawContext, camera, bounds, opacity, frameStats);
    }

    void layer_OnChanged(object sender, ChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(EditorStoryboardLayer.OsbLayer) or nameof(EditorStoryboardLayer.DiffSpecific))
            sortLayer(Unsafe.As<EditorStoryboardLayer>(sender));
    }

    void sortLayer(EditorStoryboardLayer layer)
    {
        var initialIndex = layers.IndexOf(layer);
        if (initialIndex < 0) throw new InvalidOperationException($"Layer '{layer.Name}' cannot be found");

        var newIndex = initialIndex;
        while (newIndex > 0 && layer.CompareTo(layers[newIndex - 1]) < 0) --newIndex;
        while (newIndex < layers.Count - 1 && layer.CompareTo(layers[newIndex + 1]) > 0) ++newIndex;

        layers.Move(initialIndex, newIndex);
        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    int findLayerIndex(EditorStoryboardLayer layer)
    {
        var index = layers.BinarySearch(layer);
        if (index < 0) return ~index;
        while (index < layers.Count && layer.CompareTo(layers[index]) == 0) ++index;
        return index;
    }
}