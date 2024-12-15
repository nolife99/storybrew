namespace StorybrewEditor.Storyboarding;

using System;
using System.Collections.Generic;
using System.Linq;
using StorybrewCommon.Storyboarding;

public abstract class Effect : IDisposable
{
    public EffectConfig Config = new();

    public long EstimatedSize;

    public bool Highlight;
    List<EditorStoryboardLayer> layers;

    string name = "Unnamed Effect";
    EditorStoryboardLayer placeHolderLayer;
    public Project Project;

    public Effect(Project project)
    {
        Project = project;

        layers = [placeHolderLayer = new("", this)];
        refreshLayerNames();
        Project.LayerManager.Add(placeHolderLayer);
    }

    public string Name
    {
        get => name;
        set
        {
            if (name == value) return;

            name = value;
            RaiseChanged();
            refreshLayerNames();
        }
    }

    public abstract string BaseName { get; }
    public virtual string Path => null;

    public virtual EffectStatus Status { get; }
    public virtual string StatusMessage { get; }

    public virtual bool Multithreaded { get; }
    public virtual bool BeatmapDependent { get; }

    public float StartTime
    {
        get
        {
            var min = float.MaxValue;
            foreach (var l in layers) min = Math.Min(l.StartTime, min);
            return min == float.MaxValue ? 0 : min;
        }
    }

    public float EndTime
    {
        get
        {
            var max = float.MinValue;
            foreach (var l in layers) max = Math.Max(l.EndTime, max);
            return max == float.MinValue ? 0 : max;
        }
    }

    public event EventHandler OnChanged;
    protected void RaiseChanged() => OnChanged?.Invoke(this, EventArgs.Empty);
    public event EventHandler OnConfigFieldsChanged;
    protected void RaiseConfigFieldsChanged() => OnConfigFieldsChanged?.Invoke(this, EventArgs.Empty);

    public void AddPlaceholder(EditorStoryboardLayer layer)
    {
        if (placeHolderLayer is not null)
        {
            layers.Remove(placeHolderLayer);
            Project.LayerManager.Remove(placeHolderLayer);
            placeHolderLayer = null;
        }

        layers.Add(layer);
        refreshLayerNames();

        Project.LayerManager.Add(layer);
    }

    protected void UpdateLayers(List<EditorStoryboardLayer> newLayers)
    {
        if (placeHolderLayer is not null)
        {
            Project.LayerManager.Replace(placeHolderLayer, newLayers);
            placeHolderLayer = null;
        }
        else Project.LayerManager.Replace(layers, newLayers);

        layers = newLayers;
        refreshLayerNames();

        EstimatedSize = layers.Sum(layer => layer.EstimatedSize);
        RaiseChanged();
    }

    public void Refresh()
    {
        if (Project.Disposed) return;
        Project.QueueEffectUpdate(this);
    }

    public abstract void Update();

    void refreshLayerNames()
    {
        foreach (var layer in layers) layer.Identifier = string.IsNullOrWhiteSpace(layer.Name) ? name : $"{name} ({layer.Name})";
    }

    #region IDisposable Support

    protected bool Disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed || !disposing) return;
        foreach (var l in layers) Project.LayerManager.Remove(l);
        Disposed = true;
    }
    public void Dispose() => Dispose(true);

    #endregion
}

public enum EffectStatus
{
    Initializing,
    Loading,
    Configuring,
    Updating,
    ReloadPending,
    Ready,
    CompilationFailed,
    LoadingFailed,
    ExecutionFailed
}