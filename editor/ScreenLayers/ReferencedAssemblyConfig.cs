﻿namespace StorybrewEditor.ScreenLayers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BrewLib.UserInterface;
using BrewLib.Util;
using Storyboarding;

public class ReferencedAssemblyConfig(Project project) : UiScreenLayer
{
    static readonly KeyValuePair<string, string>[] fileFilter = [new(".NET Assemblies", "dll")];

    readonly HashSet<string> selectedAssemblies =
        project.ImportedAssemblies as HashSet<string> ?? project.ImportedAssemblies.ToHashSet();

    LinearLayout layout, assembliesLayout;
    Button okButton, cancelButton;

    public override bool IsPopup => true;

    public override void Load()
    {
        base.Load();
        Button addAssemblyButton;

        WidgetManager.Root.Add(layout = new(WidgetManager)
        {
            StyleName = "panel",
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children =
            [
                new Label(WidgetManager) { Text = "Imported Referenced Assemblies", CanGrow = false },
                new ScrollArea(WidgetManager, assembliesLayout = new(WidgetManager) { FitChildren = true }),
                addAssemblyButton = new(WidgetManager)
                {
                    Text = "Add assembly file",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false
                },
                new LinearLayout(WidgetManager)
                {
                    Horizontal = true,
                    Fill = true,
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false,
                    Children =
                    [
                        okButton = new(WidgetManager) { Text = "Ok", AnchorFrom = BoxAlignment.Centre },
                        cancelButton = new(WidgetManager)
                        {
                            Text = "Cancel", AnchorFrom = BoxAlignment.Centre
                        }
                    ]
                }
            ]
        });

        addAssemblyButton.OnClick += (_, _) => WidgetManager.ScreenLayerManager.OpenFilePicker("",
            project.ProjectFolderPath,
            fileFilter,
            path =>
            {
                if (!isValidAssembly(path))
                {
                    WidgetManager.ScreenLayerManager.ShowMessage(
                        "Invalid assembly file. Are you sure that the file is made for .NET?");

                    return;
                }

                if (validateAssembly(path))
                    addReferencedAssembly(PathHelper.FolderContainsPath(project.ProjectFolderPath, path) ?
                        path :
                        copyReferencedAssembly(path));
            });

        okButton.OnClick += (_, _) =>
        {
            project.ImportedAssemblies = selectedAssemblies;
            Exit();
        };

        cancelButton.OnClick += (_, _) => Exit();

        refreshAssemblies();
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        layout.Pack(Math.Min(400, width), Math.Min(600, height));
    }

    void refreshAssemblies()
    {
        assembliesLayout.ClearWidgets();
        foreach (var assembly in selectedAssemblies.OrderBy(getAssemblyName))
        {
            Button editButton, removeButton;
            assembliesLayout.Add(new LinearLayout(WidgetManager)
            {
                AnchorFrom = BoxAlignment.Centre,
                AnchorTo = BoxAlignment.Centre,
                Horizontal = true,
                FitChildren = true,
                Fill = true,
                Children =
                [
                    new LinearLayout(WidgetManager)
                    {
                        StyleName = "condensed",
                        Children =
                        [
                            new Label(WidgetManager)
                            {
                                StyleName = "listItem",
                                Text = getAssemblyName(assembly),
                                AnchorFrom = BoxAlignment.Left,
                                AnchorTo = BoxAlignment.Left
                            }
                        ]
                    },
                    new Button(WidgetManager)
                    {
                        StyleName = "icon",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false,
                        Displayed = false
                    },
                    editButton = new(WidgetManager)
                    {
                        StyleName = "icon",
                        Icon = IconFont.DriveFileMove,
                        Tooltip = "Change file",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false
                    },
                    removeButton = new(WidgetManager)
                    {
                        StyleName = "icon",
                        Icon = IconFont.Close,
                        Tooltip = "Remove",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false
                    }
                ]
            });

            var ass = assembly;

            editButton.OnClick += (_, _) => changeReferencedAssembly(ass);
            removeButton.OnClick += (_, _) => WidgetManager.ScreenLayerManager.ShowMessage(
                $"Remove {getAssemblyName(ass)}?",
                () => removeReferencedAssembly(ass),
                true);
        }
    }

    static string getAssemblyName(string assemblyPath)
    {
        try
        {
            return AssemblyName.GetAssemblyName(assemblyPath).Name;
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(assemblyPath);
        }
    }

    string getRelativePath(string assembly) => PathHelper.FolderContainsPath(project.ProjectFolderPath, assembly) ?
        assembly :
        Path.Combine(project.ProjectFolderPath, Path.GetFileName(assembly));

    static bool isValidAssembly(string assembly)
    {
        try
        {
            AssemblyName.GetAssemblyName(assembly);
        }
        catch
        {
            return false;
        }

        return true;
    }

    static bool assemblyImported(string assembly, IEnumerable<string> assemblies)
        => assemblies.Select(getAssemblyName).Contains(getAssemblyName(assembly));

    static bool isDefaultAssembly(string assembly)
        => Project.DefaultAssemblies.Any(ass => getAssemblyName(ass) == getAssemblyName(assembly));

    static bool validateAssembly(string assembly, IEnumerable<string> assemblies)
        => !(isDefaultAssembly(assembly) || assemblyImported(assembly, assemblies));

    bool validateAssembly(string assembly) => validateAssembly(assembly, selectedAssemblies);

    string copyReferencedAssembly(string assembly)
    {
        var newPath = getRelativePath(assembly);
        File.Copy(assembly, newPath, true);
        return newPath;
    }

    void addReferencedAssembly(string assembly)
    {
        selectedAssemblies.Add(assembly);
        refreshAssemblies();
    }

    void removeReferencedAssembly(string assembly)
    {
        selectedAssemblies.Remove(assembly);
        refreshAssemblies();
    }

    void changeReferencedAssembly(string assembly) => WidgetManager.ScreenLayerManager.OpenFilePicker("",
        Path.GetDirectoryName(assembly),
        fileFilter,
        path =>
        {
            if (!isValidAssembly(path))
            {
                WidgetManager.ScreenLayerManager.ShowMessage(
                    "Invalid assembly file. Are you sure that the file is intended for .NET?");

                return;
            }

            if (!validateAssembly(path, selectedAssemblies.Where(ass => ass != assembly))) return;

            var newPath = PathHelper.FolderContainsPath(project.ProjectFolderPath, path) ?
                path :
                copyReferencedAssembly(path);

            if (path == assembly) return;

            selectedAssemblies.Remove(assembly);
            selectedAssemblies.Add(newPath);
            refreshAssemblies();
        });
}