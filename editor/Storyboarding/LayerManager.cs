namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;
using SixLabors.ImageSharp;
using StorybrewCommon.Storyboarding;

public class LayerManager
{
    public int LayersCount => Layers.Count;
    public List<EditorStoryboardLayer> Layers { get; } = [];

    public List<EditorStoryboardLayer> FindLayers(Predicate<EditorStoryboardLayer> predicate) => Layers.FindAll(predicate);

    public event EventHandler OnLayersChanged;

    public void Add(EditorStoryboardLayer layer)
    {
        Layers.Insert(findLayerIndex(layer), layer);
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
                var index = Layers.IndexOf(oldLayer);
                if (index != -1)
                {
                    newLayer.CopySettings(Layers[index]);
                    Layers[index] = newLayer;
                }

                oldLayers.Remove(oldLayer);
            }
            else Layers.Insert(findLayerIndex(newLayer), newLayer);

            newLayer.OnChanged += layer_OnChanged;
        }

        foreach (var oldLayer in oldLayers)
        {
            oldLayer.OnChanged -= layer_OnChanged;
            Layers.Remove(oldLayer);
        }

        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(EditorStoryboardLayer oldLayer, List<EditorStoryboardLayer> newLayers)
    {
        var index = Layers.IndexOf(oldLayer);
        if (index != -1)
        {
            foreach (var newLayer in newLayers)
            {
                newLayer.CopySettings(oldLayer);
                newLayer.OnChanged += layer_OnChanged;
            }

            Layers.InsertRange(index, newLayers);

            oldLayer.OnChanged -= layer_OnChanged;
            Layers.Remove(oldLayer);
        }
        else
            throw new InvalidOperationException(
                $"Cannot replace layer '{oldLayer.Name}' with multiple layers, old layer not found");

        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(EditorStoryboardLayer layer)
    {
        if (!Layers.Remove(layer)) return;

        layer.OnChanged -= layer_OnChanged;
        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MoveToOsbLayer(EditorStoryboardLayer layer, OsbLayer osbLayer)
    {
        var firstLayer = Layers.Find(l => l.OsbLayer == osbLayer);
        if (firstLayer is not null) MoveToLayer(layer, firstLayer);
        else layer.OsbLayer = osbLayer;
    }

    public void MoveToLayer(EditorStoryboardLayer layerToMove, EditorStoryboardLayer toLayer)
    {
        layerToMove.OsbLayer = toLayer.OsbLayer;

        var fromIndex = Layers.IndexOf(layerToMove);
        var toIndex = Layers.IndexOf(toLayer);
        if (fromIndex != -1 && toIndex != -1)
        {
            Layers.Move(fromIndex, toIndex);
            sortLayer(layerToMove);
        }
        else
            throw new InvalidOperationException(
                $"Cannot move layer '{layerToMove.Name}' to the position of '{
                    layerToMove.Name}'");
    }

    public void TriggerEvents(float startTime, float endTime)
    {
        foreach (var layer in Layers) layer.TriggerEvents(startTime, endTime);
    }

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity, FrameStats frameStats)
    {
        foreach (var layer in Layers) layer.Draw(drawContext, camera, bounds, opacity, frameStats);
    }

    void layer_OnChanged(object sender, ChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(EditorStoryboardLayer.OsbLayer) or nameof(EditorStoryboardLayer.DiffSpecific))
            sortLayer(Unsafe.As<EditorStoryboardLayer>(sender));
    }

    void sortLayer(EditorStoryboardLayer layer)
    {
        var initialIndex = Layers.IndexOf(layer);
        if (initialIndex < 0) throw new InvalidOperationException($"Layer '{layer.Name}' cannot be found");

        var newIndex = initialIndex;
        while (newIndex > 0 && layer.CompareTo(Layers[newIndex - 1]) < 0) --newIndex;

        while (newIndex < Layers.Count - 1 && layer.CompareTo(Layers[newIndex + 1]) > 0) ++newIndex;

        Layers.Move(initialIndex, newIndex);
        OnLayersChanged?.Invoke(this, EventArgs.Empty);
    }

    int findLayerIndex(EditorStoryboardLayer layer)
    {
        var index = Layers.BinarySearch(layer);
        if (index < 0) return ~index;

        while (index < Layers.Count && layer.CompareTo(Layers[index]) == 0) ++index;

        return index;
    }
}