namespace StorybrewEditor.Storyboarding;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewEditor.Scripting;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class ScriptedEffect : Effect
{
    readonly ScriptContainer<StoryboardObjectGenerator> scriptContainer;

    bool beatmapDependent = true;
    int configScriptIdentifier;
    MultiFileWatcher dependencyWatcher;

    bool multithreaded;

    EffectStatus status = EffectStatus.Initializing;
    PooledList<char> statusMessage;

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
        => statusMessage is null ? default : statusMessage.AsReadOnlySpan();

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

        var success = false;
        try
        {
            Interlocked.Exchange(ref token, cts);

            await changeStatus(EffectStatus.Loading);
            var script = scriptContainer.CreateScript(cts);

            await changeStatus(EffectStatus.Configuring);
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

            script.Generate(context, ControlledExecution.Run, cts.Token);

            foreach (var layer in context.EditorLayers) layer.PostProcess();

            success = true;
        }
        catch (ScriptCompilationException e)
        {
            await changeStatus(EffectStatus.CompilationFailed, e.Message, context.Log);
            return;
        }
        catch (ScriptLoadingException e)
        {
            if (e.InnerException is null) await changeStatus(EffectStatus.LoadingFailed, e.Message, context.Log);
            else
            {
                ValueTask task;
                using (var msg = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                    $"{e.Message}: {e.InnerException.Message}"))
                    task = changeStatus(EffectStatus.LoadingFailed, msg.AsReadOnlySpan(), context.Log);

                await task;
            }

            return;
        }
        catch (OperationCanceledException)
        {
            await changeStatus(EffectStatus.UpdateCanceled);
            return;
        }
        catch (Exception e)
        {
            ValueTask task;
            using (var msg = getExecutionFailedMessage(e))
                task = changeStatus(EffectStatus.ExecutionFailed, msg.AsReadOnlySpan(), context.Log);

            await task;
            return;
        }
        finally
        {
            if (!success)
            {
                if (dependencyWatcher is not null)
                {
                    dependencyWatcher.Watch(newDependencyWatcher.WatchedFilenames);

                    newDependencyWatcher.Dispose();
                    newDependencyWatcher = null;
                }
                else dependencyWatcher = newDependencyWatcher;
            }
        }

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

    public override void CancelUpdate() => Interlocked.Exchange(ref token, null).CancelAfter(400);

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

        statusMessage ??= new();
        statusMessage.Clear();

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

    TempList<char> getExecutionFailedMessage(Exception e)
        => e is FileNotFoundException exception ?
            StringHelper.Interpolate(CultureInfo.InvariantCulture,
                $"File not found while {status}. Verify this path is valid:\n{exception.FileName}\n\nDetails:\n{e}") :
            StringHelper.Interpolate(CultureInfo.InvariantCulture, $"Uncaught error during {status}:\n{e}");

    #region IDisposable Support

    bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                statusMessage?.Dispose();
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