using BrewLib.ScreenLayers;
using BrewLib.Util;
using Microsoft.Win32;
using StorybrewEditor.ScreenLayers.Util;
using StorybrewEditor.Storyboarding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StorybrewEditor.ScreenLayers;

public static class ScreenLayerManagerExtensions
{
    public static void OpenFolderPicker(this ScreenLayerManager screenLayer, string description, string initialValue, Action<string> callback)
        => screenLayer.AsyncLoading("Select a folder", () =>
    {
        OpenFolderDialog dialog = new()
        {
            Title = description,
            FolderName = initialValue,
            InitialDirectory = initialValue
        };
        System.Windows.Window parent = new()
        {
            Topmost = true,
            ResizeMode = 0,
            WindowStyle = 0,
            Visibility = System.Windows.Visibility.Hidden,
            Content = dialog
        };

        var result = false;
        parent.Loaded += (s, e) => result = dialog.ShowDialog().Value;
        parent.ShowDialog();
        parent.Close();

        if (result) Program.Schedule(() => callback(dialog.FolderName));
    });
    public static void OpenFilePicker(this ScreenLayerManager screenLayer, string description, string initialValue, string initialDirectory, string filter, Action<string> callback)
        => screenLayer.AsyncLoading("Select a file", () =>
    {
        OpenFileDialog dialog = new()
        {
            Title = description,
            FileName = initialValue,
            Filter = filter,
            InitialDirectory = initialDirectory is not null ? Path.GetFullPath(initialDirectory) : ""
        };
        System.Windows.Window parent = new()
        {
            Topmost = true,
            ResizeMode = 0,
            WindowStyle = 0,
            Visibility = System.Windows.Visibility.Hidden,
            Content = dialog
        };

        var result = false;
        parent.Loaded += (s, e) => result = dialog.ShowDialog().Value;
        parent.ShowDialog();
        parent.Close();

        if (result) Program.Schedule(() => callback(dialog.FileName));
    });
    public static void OpenSaveLocationPicker(this ScreenLayerManager screenLayer, string description, string initialValue, string extension, string filter, Action<string> callback)
        => screenLayer.AsyncLoading("Select a location", () =>
    {
        SaveFileDialog dialog = new()
        {
            Title = description,
            RestoreDirectory = true,
            FileName = initialValue,
            OverwritePrompt = true,
            DefaultExt = extension,
            Filter = filter
        };
        System.Windows.Window parent = new()
        {
            Topmost = true,
            ResizeMode = 0,
            WindowStyle = 0,
            Visibility = System.Windows.Visibility.Hidden,
            Content = dialog
        };

        var result = false;
        parent.Loaded += (s, e) => result = dialog.ShowDialog().Value;
        parent.ShowDialog();
        parent.Close();

        if (result) Program.Schedule(() => callback(dialog.FileName));
    });

    public static void AsyncLoading(this ScreenLayerManager screenLayer, string message, Action action)
        => screenLayer.Add(new LoadingScreen(message, action));

    public static void ShowMessage(this ScreenLayerManager screenLayer, string message, Action ok = null)
        => screenLayer.Add(new MessageBox(message, ok));

    public static void ShowMessage(this ScreenLayerManager screenLayer, string message, Action ok, bool cancel)
        => screenLayer.Add(new MessageBox(message, ok, cancel));

    public static void ShowMessage(this ScreenLayerManager screenLayer, string message, Action yes, Action no, bool cancel)
        => screenLayer.Add(new MessageBox(message, yes, no, cancel));

    public static void ShowPrompt(this ScreenLayerManager screenLayer, string title, Action<string> action)
        => screenLayer.Add(new PromptBox(title, "", "", action));

    public static void ShowPrompt(this ScreenLayerManager screenLayer, string title, string description, Action<string> action)
        => screenLayer.Add(new PromptBox(title, description, "", action));

    public static void ShowPrompt(this ScreenLayerManager screenLayer, string title, string description, string text, Action<string> action)
        => screenLayer.Add(new PromptBox(title, description, text, action));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer, string title, Action<T> action, params ContextMenu<T>.Option[] options)
        => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer, string title, Action<T> action, params T[] options)
        => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowContextMenu<T>(this ScreenLayerManager screenLayer, string title, Action<T> action, IEnumerable<T> options)
        => screenLayer.Add(new ContextMenu<T>(title, action, options));

    public static void ShowOpenProject(this ScreenLayerManager screenLayer)
    {
        if (!Directory.Exists(Project.ProjectsFolder)) Directory.CreateDirectory(Project.ProjectsFolder);

        screenLayer.OpenFilePicker("", "", Project.ProjectsFolder, Project.FileFilter, projectPath =>
        {
            if (!PathHelper.FolderContainsPath(Project.ProjectsFolder, projectPath) || PathHelper.GetRelativePath(
                Project.ProjectsFolder, projectPath).Count(c => c == '/') != 1)
                screenLayer.ShowMessage("Projects must be placed in a folder directly inside the 'projects' folder.");

            else screenLayer.AsyncLoading("Loading project", () =>
            {
                var resourceContainer = screenLayer.GetContext<Editor>().ResourceContainer;

                var project = Project.Load(projectPath, true, resourceContainer);
                Program.Schedule(() => screenLayer.Set(new ProjectMenu(project)));
            });
        });
    }
}