﻿namespace StorybrewEditor.ScreenLayers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using BrewLib.ScreenLayers;
using BrewLib.Util;
using Storyboarding;
using Util;
using MessageBox = Util.MessageBox;

public static class ScreenLayerManagerExtensions
{
    public static void OpenFolderPicker(this ScreenLayerManager screenLayer,
        string description,
        string initialValue,
        Action<string> callback) => screenLayer.AsyncLoading("Select a folder", () =>
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = description, ShowNewFolderButton = true, SelectedPath = initialValue
        };

        if (dialog.ShowDialog(screenLayer.GetContext<Editor>().FormsWindow) is DialogResult.OK)
            Program.Schedule(() => callback(dialog.SelectedPath));
    });

    public static void OpenFilePicker(this ScreenLayerManager screenLayer,
        string description,
        string initialValue,
        string initialDirectory,
        string filter,
        Action<string> callback) => screenLayer.AsyncLoading("Select a file", () =>
    {
        using OpenFileDialog dialog = new()
        {
            Title = description,
            RestoreDirectory = true,
            ShowHelp = false,
            FileName = initialValue,
            Filter = filter,
            InitialDirectory = initialDirectory is not null ? Path.GetFullPath(initialDirectory) : ""
        };

        if (dialog.ShowDialog(screenLayer.GetContext<Editor>().FormsWindow) is DialogResult.OK)
            Program.Schedule(() => callback(dialog.FileName));
    });

    public static void OpenSaveLocationPicker(this ScreenLayerManager screenLayer,
        string description,
        string initialValue,
        string extension,
        string filter,
        Action<string> callback) => screenLayer.AsyncLoading("Select a location", () =>
    {
        using SaveFileDialog dialog = new()
        {
            Title = description,
            RestoreDirectory = true,
            FileName = initialValue,
            OverwritePrompt = true,
            DefaultExt = extension,
            Filter = filter
        };

        if (dialog.ShowDialog(screenLayer.GetContext<Editor>().FormsWindow) is DialogResult.OK)
            Program.Schedule(() => callback(dialog.FileName));
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
        screenLayer.OpenFilePicker("", "", Project.ProjectsFolder, Project.FileFilter, projectPath =>
        {
            if (!PathHelper.FolderContainsPath(Project.ProjectsFolder, projectPath) ||
                Path.GetRelativePath(Project.ProjectsFolder, projectPath).Contains('/'))
                screenLayer.ShowMessage("Projects must be placed in a folder directly inside the 'projects' folder.");

            else
                screenLayer.AsyncLoading("Loading project", () =>
                {
                    var project = Project.Load(projectPath, true, screenLayer.GetContext<Editor>().ResourceContainer);
                    Program.Schedule(() => screenLayer.Set(new ProjectMenu(project)));
                });
        });
    }
}