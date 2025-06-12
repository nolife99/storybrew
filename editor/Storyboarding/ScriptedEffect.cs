namespace StorybrewEditor.Storyboarding;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading;
using Scripting;
using StorybrewCommon.Scripting;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Util;

public class ScriptedEffect : Effect
{
    readonly ScriptContainer<StoryboardObjectGenerator> scriptContainer;

    bool beatmapDependent = true;
    string configScriptIdentifier;
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
        statusStopwatch = Stopwatch.GetTimestamp();

        this.scriptContainer = scriptContainer;
        scriptContainer.OnScriptChanged += scriptContainer_OnScriptChanged;

        this.multithreaded = multithreaded;
    }

    public override string BaseName => scriptContainer?.Name;
    public override string Path => scriptContainer?.MainSourcePath;
    public override EffectStatus Status => status;
    public override ReadOnlySpan<char> StatusMessage => statusMessage.AsReadOnlySpan();
    public override bool Multithreaded => multithreaded;
    public override bool BeatmapDependent => beatmapDependent;

    public override void Update(CancellationTokenSource cts)
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

            changeStatus(EffectStatus.Loading);
            var script = scriptContainer.CreateScript(cts);

            changeStatus(EffectStatus.Configuring);
            Program.Schedule(() =>
                {
                    beatmapDependent = true;
                    if (script.Identifier != configScriptIdentifier)
                    {
                        script.UpdateConfiguration(Config);
                        configScriptIdentifier = script.Identifier;

                        RaiseConfigFieldsChanged();
                    }
                    else script.ApplyConfiguration(Config);
                })
                .Wait();

            changeStatus(EffectStatus.Updating);

            ControlledExecution.Run(() => script.Generate(context), cts.Token);

            foreach (var layer in context.EditorLayers) layer.PostProcess();

            success = true;
        }
        catch (ScriptCompilationException e)
        {
            changeStatus(EffectStatus.CompilationFailed, e.Message, context.Log);
            return;
        }
        catch (ScriptLoadingException e)
        {
            changeStatus(EffectStatus.LoadingFailed,
                e.InnerException is not null ? $"{e.Message}: {e.InnerException.Message}" : e.Message,
                context.Log);

            return;
        }
        catch (OperationCanceledException)
        {
            changeStatus(EffectStatus.UpdateCanceled);
            return;
        }
        catch (Exception e)
        {
            changeStatus(EffectStatus.ExecutionFailed, getExecutionFailedMessage(e), context.Log);
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

        changeStatus(EffectStatus.Ready, log: context.Log);
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

        Program.Schedule(() =>
        {
            UpdateLayers(new(context.EditorLayers));
            context.Dispose();
        });
    }

    public override void CancelUpdate()
    {
        var localToken = token;
        Interlocked.Exchange(ref token, null);

        localToken.CancelAfter(400);
    }

    void scriptContainer_OnScriptChanged(object sender, EventArgs e) => Refresh();

    void changeStatus(EffectStatus status, ReadOnlySpan<char> message = default, ReadOnlySpan<char> log = default)
    {
        var duration = Stopwatch.GetElapsedTime(statusStopwatch);
        if (duration > TimeSpan.Zero)
            switch (this.status)
            {
                case EffectStatus.Ready:
                case EffectStatus.CompilationFailed:
                case EffectStatus.LoadingFailed:
                case EffectStatus.ExecutionFailed: break;

                default: Trace.WriteLine($"{BaseName}: {this.status} took {duration.Milliseconds}ms"); break;
            }

        this.status = status;

        var statusMessageBuilder = ValueList<char>.Create();
        if (!message.IsEmpty) statusMessageBuilder.AddRange(message);

        if (!log.IsWhiteSpace())
        {
            if (statusMessageBuilder.Count > 0) statusMessageBuilder.AddRange("\n\n".AsSpan());

            statusMessageBuilder.AddRange("Log:\n\n".AsSpan());
            statusMessageBuilder.AddRange(log);
        }

        statusMessage.Dispose();
        statusMessage = statusMessageBuilder;

        Program.Schedule(RaiseChanged).Wait();
        statusStopwatch = Stopwatch.GetTimestamp();
    }

    string getExecutionFailedMessage(Exception e) => e is FileNotFoundException exception ?
        $"File not found while {status}. Verify this path is valid:\n{exception.FileName}\n\nDetails:\n{e}" :
        $"Uncaught error during {status}:\n{e}";

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