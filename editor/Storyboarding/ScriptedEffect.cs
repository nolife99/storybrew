namespace StorybrewEditor.Storyboarding;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading;
using BrewLib.Util;
using Scripting;
using StorybrewCommon.Scripting;
using Util;

public class ScriptedEffect : Effect
{
    readonly ScriptContainer<StoryboardObjectGenerator> scriptContainer;

    bool beatmapDependent = true;
    string configScriptIdentifier;
    MultiFileWatcher dependencyWatcher;

    bool multithreaded;

    EffectStatus status = EffectStatus.Initializing;
    string statusMessage;

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
    public override string StatusMessage => statusMessage;
    public override bool Multithreaded => multithreaded;
    public override bool BeatmapDependent => beatmapDependent;

    public override void Update(CancellationTokenSource token)
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
            Interlocked.Exchange(ref this.token, token);

            changeStatus(EffectStatus.Loading);
            var script = scriptContainer.CreateScript(token);

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

            ControlledExecution.Run(() => script.Generate(context), token.Token);

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
        catch (Exception e)
        {
            var inner = e;
            while (inner is not null)
            {
                if (inner is OperationCanceledException)
                {
                    changeStatus(EffectStatus.UpdateCanceled);
                    return;
                }

                inner = e.InnerException;
            }

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

            context.Dispose();
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

        Program.Schedule(() => UpdateLayers(context.EditorLayers));
    }

    public override void CancelUpdate()
    {
        if (token is null) return;

        var localToken = token;
        Interlocked.Exchange(ref token, null);

        localToken.Cancel(true);
    }

    void scriptContainer_OnScriptChanged(object sender, EventArgs e) => Refresh();

    void changeStatus(EffectStatus status, string message = null, string log = null)
    {
        var duration = Stopwatch.GetElapsedTime(statusStopwatch);
        if (duration > TimeSpan.Zero)
            switch (this.status)
            {
                case EffectStatus.Ready:
                case EffectStatus.UpdateCanceled:
                case EffectStatus.CompilationFailed:
                case EffectStatus.LoadingFailed:
                case EffectStatus.ExecutionFailed: break;

                default: Trace.WriteLine($"{BaseName}: {this.status} took {duration.Milliseconds}ms"); break;
            }

        this.status = status;

        var statusMessageBuilder = StringHelper.StringBuilderPool.Retrieve();
        if (message is not null) statusMessageBuilder.Append(message);

        if (!string.IsNullOrWhiteSpace(log))
        {
            if (statusMessageBuilder.Length > 0) statusMessageBuilder.Append("\n\n");

            statusMessageBuilder.Append("Log:\n\n");
            statusMessageBuilder.Append(log);
        }

        statusMessage = statusMessageBuilder.ToString();

        StringHelper.StringBuilderPool.Release(statusMessageBuilder);

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