namespace StorybrewEditor.Storyboarding;

using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewEditor.Scripting;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

public class ScriptedEffect : Effect
{
    readonly ScriptContainer<StoryboardObjectGenerator> scriptContainer;

    bool beatmapDependent = true;
    int configScriptIdentifier;
    MultiFileWatcher dependencyWatcher;

    bool multithreaded;

    EffectStatus status = EffectStatus.Initializing;
    ValueList<char> statusMessage;

    long statusStopwatch;
    CancellationTokenSource token;

    public ScriptedEffect(Project project,
        ScriptContainer<StoryboardObjectGenerator> scriptContainer,
        bool multithreaded = false) : base(project)
    {
        statusStopwatch = Environment.TickCount64;

        this.scriptContainer = scriptContainer;
        scriptContainer.OnScriptChanged += scriptContainer_OnScriptChanged;

        this.multithreaded = multithreaded;
    }

    public override ReadOnlySpan<char> BaseName => scriptContainer is null ? default : scriptContainer.Name;
    public override string Path => scriptContainer?.MainSourcePath;
    public override EffectStatus Status => status;

    public override ReadOnlySpan<char> StatusMessage
        => statusMessage.IsValid ? statusMessage.AsReadOnlySpan() : default;

    public override bool Multithreaded => multithreaded;
    public override bool BeatmapDependent => beatmapDependent;

    public override async ValueTask Update(CancellationTokenSource cts)
    {
        if (!scriptContainer.HasScript) return;

        MultiFileWatcher newDependencyWatcher = new();
        newDependencyWatcher.OnFileChanged += (_, _) =>
        {
            if (!Disposed) Refresh();
        };

        EditorGeneratorContext context = new(this,
            Project.ProjectFolderPath,
            Project.ProjectAssetFolderPath,
            Project.MapsetPath,
            Project.MainBeatmap,
            Project.MapsetManager.Beatmaps,
            newDependencyWatcher);

        Interlocked.Exchange(ref token, cts);

        await changeStatus(EffectStatus.Loading);
        var scriptResult = scriptContainer.CreateScript(cts);

        if (!scriptResult.Success)
        {
            await updateWatcherWithException(newDependencyWatcher, scriptResult.Error, context);
            return;
        }

        await changeStatus(EffectStatus.Configuring);

        var script = scriptResult.Value;
        await Program.Schedule(state =>
            {
                var (localScript, localEffect) = state;

                localEffect.beatmapDependent = true;
                if (localScript.Identifier != localEffect.configScriptIdentifier)
                {
                    localScript.UpdateConfiguration(localEffect.Config);
                    localEffect.configScriptIdentifier = localScript.Identifier;

                    localEffect.OnConfigFieldsChanged();
                }
                else localScript.ApplyConfiguration(localEffect.Config);
            },
            (script, this));

        await changeStatus(EffectStatus.Updating);

#pragma warning disable SYSLIB0046
        var scriptException = script.Generate(context, ControlledExecution.Run, cts.Token);
#pragma warning restore SYSLIB0046

        if (scriptException is not null)
        {
            await updateWatcherWithException(newDependencyWatcher, scriptException, context);
            return;
        }

        foreach (var layer in context.EditorLayers) layer.PostProcess();

        await changeStatus(EffectStatus.Ready, log: context.Log);
        if (Disposed)
        {
            newDependencyWatcher.Dispose();
            return;
        }

        multithreaded = context.Multithreaded;
        beatmapDependent = context.BeatmapDependent;
        dependencyWatcher?.Dispose();
        dependencyWatcher = newDependencyWatcher;

        if (Project.Disposed) return;

        Program.Schedule(state =>
            {
                state.Item2.UpdateLayers(state.context.EditorLayers);
                state.context.Dispose();
            },
            (context, this));
    }

    ValueTask updateWatcherWithException(MultiFileWatcher watcher, Exception ex, EditorGeneratorContext context)
    {
        if (dependencyWatcher is not null)
        {
            dependencyWatcher.Watch(watcher);
            watcher.Dispose();
        }
        else dependencyWatcher = watcher;

        switch (ex)
        {
            case OperationCanceledException: return changeStatus(EffectStatus.UpdateCanceled);
            case ScriptLoadingException: return changeStatus(EffectStatus.LoadingFailed, ex.Message, context.Log);

            case ScriptCompilationException:
                return changeStatus(EffectStatus.CompilationFailed, ex.Message, context.Log);

            default:
                using (var msg = StringHelper.Interpolate($"Uncaught error during {status}:\n{ex}"))
                    return changeStatus(EffectStatus.ExecutionFailed, msg.AsReadOnlySpan(), context.Log);
        }
    }

    public override void CancelUpdate() => Interlocked.Exchange(ref token, null).CancelAfter(200);

    void scriptContainer_OnScriptChanged(object sender, EventArgs e) => Refresh();

    ValueTask changeStatus(EffectStatus status, ReadOnlySpan<char> message = default, ReadOnlySpan<char> log = default)
    {
        var duration = Environment.TickCount64 - statusStopwatch;
        if (duration > 0)
            switch (this.status)
            {
                case EffectStatus.Ready:
                case EffectStatus.CompilationFailed:
                case EffectStatus.LoadingFailed:
                case EffectStatus.ExecutionFailed:
                    break;

                default: Trace.WriteLine($"{Name}: {this.status} took {duration}ms"); break;
            }

        this.status = status;

        if (statusMessage.IsValid) statusMessage.Clear();
        else statusMessage = ValueList.Create<char>();

        if (!message.IsEmpty) statusMessage.AddRange(message);

        if (!log.IsWhiteSpace())
        {
            if (statusMessage.Count > 0) statusMessage.AddRange("\n\n");

            statusMessage.AddRange("Log:\n\n");
            statusMessage.AddRange(log);
        }

        var task = Program.Schedule(ef => ef.OnChanged(), this);
        statusStopwatch = Environment.TickCount64;
        return task;
    }

    #region IDisposable Support

    bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                statusMessage.Dispose();
                dependencyWatcher?.Dispose();
                scriptContainer.OnScriptChanged -= scriptContainer_OnScriptChanged;
            }

            dependencyWatcher = null;
            disposed = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}