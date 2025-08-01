namespace StorybrewEditor.Storyboarding;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StorybrewCommon.Storyboarding;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public abstract class Effect : IDisposable
{
    readonly PooledList<EditorStoryboardLayer> layers;

    ValueArray<char> name = ValueArray.Create<char>("Unnamed Effect");
    EditorStoryboardLayer placeHolderLayer;

    bool willRefresh;

    public Effect(Project project)
    {
        Project = project;

        layers = [placeHolderLayer = new("", this)];
        refreshLayerNames();
        Project.LayerManager.Add(placeHolderLayer);
    }

    public EffectConfig Config { get; } = new();

    public long EstimatedSize { get; private set; }

    public bool Highlight { get; set; }
    public Project Project { get; }

    public ReadOnlySpan<char> Name
    {
        get => name.AsReadOnlySpan();
        set
        {
            if (name.AsReadOnlySpan().SequenceEqual(value)) return;

            name.Dispose();
            name = ValueArray.Create(value);

            OnChanged();
            refreshLayerNames();
        }
    }

    public abstract ReadOnlySpan<char> BaseName { get; }
    public virtual string Path => null;

    public virtual EffectStatus Status { get; }
    public virtual ReadOnlySpan<char> StatusMessage => default;

    public virtual bool Multithreaded { get; }
    public virtual bool BeatmapDependent { get; }

    public float StartTime
    {
        get
        {
            var min = float.MaxValue;
            foreach (var l in layers) min = float.Min(l.StartTime, min);
            return min == float.MaxValue ? 0 : min;
        }
    }

    public float EndTime
    {
        get
        {
            var max = float.MinValue;
            foreach (var l in layers) max = float.Max(l.EndTime, max);
            return max == float.MinValue ? 0 : max;
        }
    }

    public event EventHandler Changed, ConfigFieldsChanged;

    protected void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
    protected void OnConfigFieldsChanged() => ConfigFieldsChanged?.Invoke(this, EventArgs.Empty);

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

    protected void UpdateLayers(ReadOnlySpan<EditorStoryboardLayer> newLayers)
    {
        if (placeHolderLayer is not null)
        {
            Project.LayerManager.Replace(placeHolderLayer, newLayers);
            placeHolderLayer = null;
        }
        else Project.LayerManager.Replace(layers.AsReadOnlySpan(), newLayers);

        layers.Clear();
        layers.AddRange(newLayers);

        refreshLayerNames();

        EstimatedSize = layers.Sum(layer => layer.EstimatedSize);
        OnChanged();
    }

    public void Refresh()
    {
        if (Project.Disposed || willRefresh) return;

        if (Status is EffectStatus.Ready
            or EffectStatus.UpdateCanceled
            or EffectStatus.CompilationFailed
            or EffectStatus.LoadingFailed
            or EffectStatus.ExecutionFailed) Project.QueueEffectUpdate(this);
        else QueueStatusCheckForUpdate();
    }

    void QueueStatusCheckForUpdate()
    {
        willRefresh = true;
        Changed += OnStatusChangedForQueuedUpdate;

        return;

        void OnStatusChangedForQueuedUpdate(object sender, EventArgs e)
        {
            var ef = (Effect)sender;
            if (ef.Status is not (EffectStatus.Ready
                or EffectStatus.UpdateCanceled
                or EffectStatus.CompilationFailed
                or EffectStatus.LoadingFailed
                or EffectStatus.ExecutionFailed)) return;

            ef.Changed -= OnStatusChangedForQueuedUpdate;
            ef.Project.QueueEffectUpdate(ef);

            willRefresh = false;
        }
    }

    public abstract ValueTask Update(CancellationTokenSource cts);
    public abstract void CancelUpdate();

    void refreshLayerNames()
    {
        foreach (var layer in layers)
            layer.Identifier = string.IsNullOrWhiteSpace(layer.Name) ?
                name.AsReadOnlySpan().ToString() :
                $"{name.AsReadOnlySpan()} ({layer.Name})";
    }

    #region IDisposable Support

    private protected bool Disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed || !disposing) return;

        foreach (var l in layers) Project.LayerManager.Remove(l);
        layers.Dispose();

        name.Dispose();

        Disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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
    ExecutionFailed,
    UpdateCanceled
}