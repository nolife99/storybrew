using System;
using System.Collections.Generic;
using System.Linq;
using BrewLib.Util;
using StorybrewCommon.Storyboarding;

namespace StorybrewEditor.Storyboarding;

public abstract class Effect : IDisposable
{
    List<EditorStoryboardLayer> layers;
    EditorStoryboardLayer placeHolderLayer;

    public readonly Project Project;

    public Guid Guid = Guid.NewGuid();

    string name = "Unnamed Effect";
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
    public virtual bool BeatmapDependant { get; }

    public double StartTime
    {
        get
        {
            var min = double.MaxValue;
            layers.ForEachUnsafe(l => min = Math.Min(l.StartTime, min));
            return min == double.MaxValue ? 0 : min;
        }
    }
    public double EndTime
    {
        get
        {
            var max = double.MinValue;
            layers.ForEachUnsafe(l => max = Math.Max(l.EndTime, max));
            return max == double.MinValue ? 0 : max;
        }
    }

    public bool Highlight;

    public long EstimatedSize;

    public event EventHandler OnChanged;
    protected void RaiseChanged() => OnChanged?.Invoke(this, EventArgs.Empty);

    public EffectConfig Config = new();
    public event EventHandler OnConfigFieldsChanged;
    protected void RaiseConfigFieldsChanged() => OnConfigFieldsChanged?.Invoke(this, EventArgs.Empty);

    public Effect(Project project)
    {
        Project = project;

        layers = [(placeHolderLayer = new(string.Empty, this))];
        refreshLayerNames();
        Project.LayerManager.Add(placeHolderLayer);
    }

    ///<summary> Used at load time to let the effect know about placeholder layers it should use. </summary>
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

    ///<summary> Queues an update call. </summary>
    public void Refresh()
    {
        if (Project.Disposed) return;
        Project.QueueEffectUpdate(this);
    }

    ///<summary> Should only be called by <see cref="Project.QueueEffectUpdate(Effect)"/>. Doesn't run on the main thread. </summary>
    public abstract void Update();
    void refreshLayerNames() => layers.ForEachUnsafe(layer => layer.Identifier = string.IsNullOrWhiteSpace(layer.Name) ? $"{name}" : $"{name} ({layer.Name})");

    #region IDisposable Support

    public bool Disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!Disposed)
        {
            if (disposing) layers.ForEachUnsafe(layer => Project.LayerManager.Remove(layer));
            layers.Clear();

            layers = null;
            OnChanged = null;
            Disposed = true;
        }
    }
    public void Dispose() => Dispose(true);

    #endregion
}
public enum EffectStatus
{
    Initializing, Loading, Configuring, Updating, ReloadPending, Ready,
    CompilationFailed, LoadingFailed, ExecutionFailed
}