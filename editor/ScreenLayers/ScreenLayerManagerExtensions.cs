namespace StorybrewEditor.ScreenLayers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BrewLib.ScreenLayers;
using BrewLib.Util;
using NfdExt;
using Storyboarding;
using Util;

public static class ScreenLayerManagerExtensions
{
    public static void OpenFolderPicker(this ScreenLayerManager screenLayer, string initialValue, Action<string> callback)
        => screenLayer.AsyncLoading("Select a folder",
            async () =>
            {
                var selectedPath = NFD.PickFolder(initialValue);
                if (!string.IsNullOrEmpty(selectedPath)) await Program.Schedule(() => callback(selectedPath));
            });

    public static void OpenFilePicker(this ScreenLayerManager screenLayer,
        string initialValue,
        string initialDirectory,
        IReadOnlyCollection<KeyValuePair<string, string>> filter,
        Action<string> callback) => screenLayer.AsyncLoading("Select a file",
        async () =>
        {
            var fileName = NFD.OpenDialog(Path.Combine(initialDirectory, initialValue), filter);
            if (!string.IsNullOrEmpty(fileName)) await Program.Schedule(() => callback(fileName));
        });

    public static void OpenSaveLocationPicker(this ScreenLayerManager screenLayer,
        string initialValue,
        string extension,
        IReadOnlyCollection<KeyValuePair<string, string>> filter,
        Action<string> callback) => screenLayer.AsyncLoading("Select a location",
        async () =>
        {
            var fileName = NFD.SaveDialog(initialValue, extension, filter);
            if (!string.IsNullOrEmpty(fileName)) await Program.Schedule(() => callback(fileName));
        });

    public static void AsyncLoading(this ScreenLayerManager screenLayer, string message, Func<Task> action)
        => screenLayer.Add(new LoadingScreen(message, action));

    public static void ShowMessage(this ScreenLayerManager screenLayer, string message, Action ok = null)
        => screenLayer.Add(new MessageBox(message, ok, null, false));

    public static void ShowMessage(this ScreenLayerManager screenLayer, string message, Action ok, bool cancel)
        => screenLayer.Add(new MessageBox(message, ok, null, cancel));

    public static void ShowMessage(this ScreenLayerManager screenLayer, string message, Action yes, Action no, bool cancel)
        => screenLayer.Add(new MessageBox(message, yes, no, cancel));

    public static void ShowPrompt(this ScreenLayerManager screenLayer, string title, Action<string> action)
        => screenLayer.Add(new PromptBox(title, "", "", action));

    public static void ShowPrompt(this ScreenLayerManager screenLayer,
        string title,
        string description,
        string text,
        Action<string> action) => screenLayer.Add(new PromptBox(title, description, text, action));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer, string title, Action<T> action, params T[] options)
        => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer,
        string title,
        Action<T> action,
        IEnumerable<T> options) => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowOpenProject(this ScreenLayerManager screenLayer)
    {
        if (!Directory.Exists(Project.ProjectsFolder)) Directory.CreateDirectory(Project.ProjectsFolder);
        screenLayer.OpenFilePicker("",
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
                            var project = Project.Load(projectPath, true, screenLayer.GetContext<Editor>().ResourceContainer);
                            await Program.Schedule(() => screenLayer.Set(new ProjectMenu(project)));
                        });
            });
    }
}