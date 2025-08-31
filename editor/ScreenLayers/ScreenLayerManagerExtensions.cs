namespace StorybrewEditor.ScreenLayers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BrewLib.ScreenLayers;
using BrewLib.Util;
using SDL3;
using StorybrewEditor.ScreenLayers.Util;
using StorybrewEditor.Storyboarding;

public static class ScreenLayerManagerExtensions
{
    static readonly ValueTaskSource<string> sharedTaskSource = new(true);

    public static void OpenFolderPicker(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> initialValue,
        Action<string> callback)
    {
        var valueTaskSource = sharedTaskSource;
        valueTaskSource.Reset();

        screenLayer.AsyncLoading("Select a folder",
            async () =>
            {
                var selectedPath = await valueTaskSource.Task;
                if (!string.IsNullOrEmpty(selectedPath))
                    await Program.Schedule(s => s.callback(s.selectedPath), (callback, selectedPath));
            });

        SDL.ShowOpenFolderDialog((_, filelist, _) =>
            {
                if (filelist.Array is null) valueTaskSource.SetException(new InvalidOperationException(SDL.GetError()));
                else if (filelist.Count != 0) valueTaskSource.SetResult(filelist[0].AsSpan().ToString());
                else valueTaskSource.SetResult(null);
            },
            0,
            0,
            initialValue,
            false);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
    }

    public static void OpenFilePicker(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> initialValue,
        scoped ReadOnlySpan<char> initialDirectory,
        ReadOnlySpan<DialogFileFilter> filter,
        Action<string> callback)
    {
        var valueTaskSource = sharedTaskSource;
        valueTaskSource.Reset();

        screenLayer.AsyncLoading("Select a file",
            async () =>
            {
                var selectedPath = await valueTaskSource.Task;
                if (!string.IsNullOrEmpty(selectedPath))
                    await Program.Schedule(s => s.callback(s.selectedPath), (callback, selectedPath));
            });

        scoped ReadOnlySpan<char> path;
        if (initialValue.IsWhiteSpace())
        {
            var length = initialDirectory.Length;
            if (length > 0 && initialDirectory[length - 1] != Path.DirectorySeparatorChar)
            {
                Span<char> initialDirectorySpan = stackalloc char[length + 1];
                initialDirectory.CopyTo(initialDirectorySpan);
                initialDirectorySpan[length] = Path.DirectorySeparatorChar;

                path = initialDirectorySpan;
            }
            else path = initialDirectory;
        }
        else if (initialDirectory.IsWhiteSpace()) path = initialValue;
        else
        {
            Span<char> joinSpan = stackalloc char[255];
            if (Path.TryJoin(initialDirectory, initialValue, joinSpan, out var written)) path = joinSpan[..written];
            else path = Path.Join(initialDirectory, initialValue);
        }

        SDL.ShowOpenFileDialog((_, filelist, _) =>
            {
                if (filelist.Array is null) valueTaskSource.SetException(new InvalidOperationException(SDL.GetError()));
                else if (filelist.Count != 0) valueTaskSource.SetResult(filelist[0].AsSpan().ToString());
                else valueTaskSource.SetResult(null);
            },
            0,
            0,
            filter,
            path,
            false);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
    }

    public static void OpenSaveLocationPicker(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> initialValue,
        ReadOnlySpan<DialogFileFilter> filter,
        Action<string> callback)
    {
        var valueTaskSource = sharedTaskSource;
        valueTaskSource.Reset();

        screenLayer.AsyncLoading("Select a location",
            async () =>
            {
                var selectedPath = await valueTaskSource.Task;
                if (!string.IsNullOrEmpty(selectedPath))
                    await Program.Schedule(s => s.callback(s.selectedPath), (callback, selectedPath));
            });

        SDL.ShowSaveFileDialog((_, filelist, _) =>
            {
                if (filelist.Array is null) valueTaskSource.SetException(new InvalidOperationException(SDL.GetError()));
                else if (filelist.Count != 0) valueTaskSource.SetResult(filelist[0].AsSpan().ToString());
                else valueTaskSource.SetResult(null);
            },
            0,
            0,
            filter,
            initialValue);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
    }

    public static void AsyncLoading(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> message,
        Func<ValueTask> action)
        => screenLayer.Add(new LoadingScreen(message, action));

    public static void ShowMessage(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> message,
        Action ok = null)
        => screenLayer.Add(new MessageBox(message, ok, null, false));

    public static void ShowMessage(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> message,
        Action ok,
        bool cancel)
        => screenLayer.Add(new MessageBox(message, ok, null, cancel));

    public static void ShowMessage(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> message,
        Action yes,
        Action no,
        bool cancel)
        => screenLayer.Add(new MessageBox(message, yes, no, cancel));

    public static void ShowPrompt(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> title,
        Action<ReadOnlySpan<char>> action)
        => screenLayer.Add(new PromptBox(title, "", "", action));

    public static void ShowPrompt(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> title,
        scoped ReadOnlySpan<char> description,
        scoped ReadOnlySpan<char> text,
        Action<ReadOnlySpan<char>> action)
        => screenLayer.Add(new PromptBox(title, description, text, action));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> title,
        Action<T> action,
        params ReadOnlySpan<T> options)
        => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer,
        scoped ReadOnlySpan<char> title,
        Action<T> action,
        IEnumerable<T> options)
        => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowOpenProject(this ScreenLayerManager screenLayer)
    {
        if (!Directory.Exists(Project.ProjectsFolder)) Directory.CreateDirectory(Project.ProjectsFolder);
        OpenFilePicker(screenLayer,
            "",
            Project.ProjectsFolder,
            Project.FileFilter,
            projectPath =>
            {
                if (!PathHelper.FolderContainsPath(Project.ProjectsFolder, projectPath) ||
                    Path.GetRelativePath(Project.ProjectsFolder, projectPath).Contains('/'))
                    screenLayer.ShowMessage("Projects must be placed directly inside the 'projects' folder.");

                else
                    screenLayer.AsyncLoading("Loading project",
                        async () =>
                        {
                            var project = Project.Load(projectPath,
                                true,
                                screenLayer.GetContext<Editor>().ResourceContainer);

                            await Program.Schedule(s => s.screenLayer.Set(new ProjectMenu(s.project)),
                                (screenLayer, project));
                        });
            });
    }
}