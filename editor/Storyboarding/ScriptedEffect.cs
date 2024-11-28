namespace StorybrewEditor.Storyboarding;

using System;
using System.Diagnostics;
using System.IO;
using BrewLib.Util;
using OpenTK.Windowing.GraphicsLibraryFramework;
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

    double statusStopwatch;

    public ScriptedEffect(Project project,
        ScriptContainer<StoryboardObjectGenerator> scriptContainer,
        bool multithreaded = false) : base(project)
    {
        statusStopwatch = GLFW.GetTime();

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

    public override void Update()
    {
        if (!scriptContainer.HasScript) return;

        MultiFileWatcher newDependencyWatcher = new();
        newDependencyWatcher.OnFileChanged += (_, _) =>
        {
            if (!Disposed) Refresh();
        };

        EditorGeneratorContext context = new(this, Project.ProjectFolderPath, Project.ProjectAssetFolderPath, Project.MapsetPath,
            Project.MainBeatmap, Project.MapsetManager.Beatmaps, newDependencyWatcher);

        var success = false;
        try
        {
            changeStatus(EffectStatus.Loading);
            var script = scriptContainer.CreateScript();

            changeStatus(EffectStatus.Configuring);
            Program.RunMainThread(() =>
            {
                beatmapDependent = true;
                if (script.Identifier != configScriptIdentifier)
                {
                    script.UpdateConfiguration(Config);
                    configScriptIdentifier = script.Identifier;

                    RaiseConfigFieldsChanged();
                }
                else script.ApplyConfiguration(Config);
            });

            changeStatus(EffectStatus.Updating);

            script.Generate(context);
            foreach (var layer in context.EditorLayers) layer.PostProcess();

            success = true;
        }
        catch (ScriptCompilationException e)
        {
            Trace.TraceWarning($"Script compilation failed for {BaseName}\n{e.Message}");
            changeStatus(EffectStatus.CompilationFailed, e.Message, context.Log);
            return;
        }
        catch (ScriptLoadingException e)
        {
            Trace.TraceWarning($"Script load failed for {BaseName}\n{e}");
            changeStatus(EffectStatus.LoadingFailed,
                e.InnerException is not null ? $"{e.Message}: {e.InnerException.Message}" : e.Message, context.Log);

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

            context.Dispose();
        }

        changeStatus(EffectStatus.Ready, log: context.Log);
        Program.Schedule(() =>
        {
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

            UpdateLayers(context.EditorLayers);
        });
    }

    void scriptContainer_OnScriptChanged(object sender, EventArgs e) => Refresh();

    void changeStatus(EffectStatus status, string message = null, string log = null)
    {
        var duration = GLFW.GetTime() - statusStopwatch;
        if (duration > 0)
            switch (this.status)
            {
                case EffectStatus.Ready:
                case EffectStatus.CompilationFailed:
                case EffectStatus.LoadingFailed:
                case EffectStatus.ExecutionFailed: break;
                default: Trace.WriteLine($"{BaseName}: {this.status} took {duration * 1000:f0}ms"); break;
            }

        this.status = status;

        var statusMessageBuilder = StringHelper.StringBuilderPool.Get();
        if (message is not null) statusMessageBuilder.Append(message);

        if (!string.IsNullOrWhiteSpace(log))
        {
            if (statusMessageBuilder.Length > 0) statusMessageBuilder.Append(" \n \n");
            statusMessageBuilder.Append("Log:\n \n");
            statusMessageBuilder.Append(log);
        }

        statusMessage = statusMessageBuilder.ToString();

        StringHelper.StringBuilderPool.Return(statusMessageBuilder);

        Program.Schedule(RaiseChanged);
        statusStopwatch = GLFW.GetTime();
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