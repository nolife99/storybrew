using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewEditor.Storyboarding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StorybrewEditor.ScreenLayers;

public class ReferencedAssemblyConfig(Project project) : UiScreenLayer
{
    LinearLayout layout, assembliesLayout, buttonsLayout;
    Button okButton, cancelButton;

    public override bool IsPopup => true;
    readonly HashSet<string> selectedAssemblies = new(project.ImportedAssemblies);

    public override void Load()
    {
        base.Load();
        Button addAssemblyButton, addSystemAssemblyButton;

        WidgetManager.Root.Add(layout = new(WidgetManager)
        {
            StyleName = "panel",
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children = new Widget[]
            {
                new Label(WidgetManager)
                {
                    Text = "Imported Referenced Assemblies",
                    CanGrow = false
                },
                new ScrollArea(WidgetManager, assembliesLayout = new(WidgetManager)
                {
                    FitChildren = true
                }),
                addAssemblyButton = new(WidgetManager)
                {
                    Text = "Add assembly file",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false
                },
                addSystemAssemblyButton = new(WidgetManager)
                {
                    Text = "Add system assembly",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false
                },
                buttonsLayout = new(WidgetManager)
                {
                    Horizontal = true,
                    Fill = true,
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false,
                    Children = new Widget[]
                    {
                        okButton = new(WidgetManager)
                        {
                            Text = "Ok",
                            AnchorFrom = BoxAlignment.Centre
                        },
                        cancelButton = new(WidgetManager)
                        {
                            Text = "Cancel",
                            AnchorFrom = BoxAlignment.Centre
                        }
                    }
                }
            }
        });

        addAssemblyButton.OnClick += (sender, e) => WidgetManager.ScreenLayerManager.OpenFilePicker("", "", project.ProjectFolderPath, ".NET Assemblies (*.dll)|*.dll", path =>
        {
            if (!isValidAssembly(path))
            {
                WidgetManager.ScreenLayerManager.ShowMessage("Invalid assembly file. Are you sure that the file is made for .NET?");
                return;
            }
            if (validateAssembly(path)) addReferencedAssembly(
                isSystemAssembly(path) ? Path.GetFileName(path) : PathHelper.FolderContainsPath(project.ProjectFolderPath, path) ? path : copyReferencedAssembly(path));
        });
        addSystemAssemblyButton.OnClick += (sender, e) => tryCatchSystemAssemblies(() =>
        {
            var systemAssemblies = getAvailableSystemAssemblies();
            WidgetManager.ScreenLayerManager.ShowContextMenu<string>("Select Assembly", result =>
            {
                var path = $"{result}.dll";
                if (validateAssembly(path)) addReferencedAssembly(path);
            }, systemAssemblies);
        });
        okButton.OnClick += (sender, e) =>
        {
            project.ImportedAssemblies = selectedAssemblies;
            Exit();
        };
        cancelButton.OnClick += (sender, e) => Exit();

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
        foreach (var assembly in selectedAssemblies.OrderBy(id => isSystemAssembly(id) ? $"_{id}" : getAssemblyName(id)))
        {
            LinearLayout assemblyRoot;
            Label nameLabel;
            Button statusButton, editButton, removeButton;

            assembliesLayout.Add(assemblyRoot = new(WidgetManager)
            {
                AnchorFrom = BoxAlignment.Centre,
                AnchorTo = BoxAlignment.Centre,
                Horizontal = true,
                FitChildren = true,
                Fill = true,
                Children = new Widget[]
                {
                    new LinearLayout(WidgetManager)
                    {
                        StyleName = "condensed",
                        Children = new Widget[]
                        {
                            nameLabel = new(WidgetManager)
                            {
                                StyleName = "listItem",
                                Text = getAssemblyName(assembly),
                                AnchorFrom = BoxAlignment.Left,
                                AnchorTo = BoxAlignment.Left
                            }
                        }
                    },
                    statusButton = new(WidgetManager)
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
                        Icon = IconFont.PencilSquare,
                        Tooltip = "Change file",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false
                    },
                    removeButton = new(WidgetManager)
                    {
                        StyleName = "icon",
                        Icon = IconFont.Times,
                        Tooltip = "Remove",
                        AnchorFrom = BoxAlignment.Centre,
                        AnchorTo = BoxAlignment.Centre,
                        CanGrow = false
                    }
                }
            });

            var ass = assembly;

            editButton.OnClick += (sender, e) => changeReferencedAssembly(ass);
            removeButton.OnClick += (sender, e) => WidgetManager.ScreenLayerManager.ShowMessage($"Remove {getAssemblyName(ass)}?", () => removeReferencedAssembly(ass), true);
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
    string getRelativePath(string assembly) => PathHelper.FolderContainsPath(project.ProjectFolderPath, assembly) ? assembly : Path.Combine(project.ProjectFolderPath, Path.GetFileName(assembly));

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

    static bool assemblyImported(string assembly, IEnumerable<string> assemblies) => assemblies.Select(getAssemblyName).Contains(getAssemblyName(assembly));
    static bool isDefaultAssembly(string assembly) => Project.DefaultAssemblies.Any(ass => getAssemblyName(ass) == getAssemblyName(assembly));
    static bool isSystemAssembly(string assemblyId) => getAssemblyName(assemblyId).StartsWith("System.", StringComparison.Ordinal);
    static bool validateAssembly(string assembly, IEnumerable<string> assemblies) => !(isDefaultAssembly(assembly) || assemblyImported(assembly, assemblies));
    bool validateAssembly(string assembly) => validateAssembly(assembly, selectedAssemblies);

    IEnumerable<string> getAvailableSystemAssemblies()
    {
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        var formsDir = Path.GetDirectoryName(typeof(Brush).Assembly.Location);
        string[] badSystemAssemblySuffixes = ["xml"];

        var allFiles = Directory.EnumerateFiles(formsDir).Union(Directory.EnumerateFiles(coreDir));

        List<string> systemAssemblies = [];
        foreach (var file in allFiles)
        {
            var assembly = Path.GetFileNameWithoutExtension(file);

            if (!assembly.StartsWith("System.", StringComparison.Ordinal)) continue;
            if (badSystemAssemblySuffixes.Any(suffix => assembly.EndsWith(suffix, StringComparison.Ordinal))) continue;

            var filename = $"{assembly}.dll";
            if (Project.DefaultAssemblies.Contains(filename)) continue;
            if (selectedAssemblies.Contains(filename)) continue;

            systemAssemblies.Add(assembly);
        }
        return systemAssemblies.OrderBy(e => e);
    }
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
    void changeReferencedAssembly(string assembly)
    {
        if (isSystemAssembly(assembly)) tryCatchSystemAssemblies(() =>
        {
            var systemAssemblies = getAvailableSystemAssemblies();
            WidgetManager.ScreenLayerManager.ShowContextMenu<string>("Select Assembly", result =>
            {
                var newPath = $"{result}.dll";
                var assemblies = selectedAssemblies.Where(ass => ass != assembly);
                if (validateAssembly(newPath, assemblies))
                {
                    selectedAssemblies.Remove(assembly);
                    selectedAssemblies.Add(newPath);
                    refreshAssemblies();
                }
            }, systemAssemblies);
        });
        else WidgetManager.ScreenLayerManager.OpenFilePicker("", "", Path.GetDirectoryName(assembly), ".NET Assemblies (*.dll)|*.dll", path =>
        {
            if (!isValidAssembly(path))
            {
                WidgetManager.ScreenLayerManager.ShowMessage("Invalid assembly file. Are you sure that the file is intended for .NET?");
                return;
            }

            var assemblies = selectedAssemblies.Where(ass => ass != assembly).ToList();

            if (validateAssembly(path, assemblies))
            {
                var newPath = PathHelper.FolderContainsPath(project.ProjectFolderPath, path) ? path : copyReferencedAssembly(path);
                if (path == assembly) return;

                selectedAssemblies.Remove(assembly);
                selectedAssemblies.Add(newPath);
                refreshAssemblies();
            }
        });
    }
    void tryCatchSystemAssemblies(Action action)
    {
        try
        {
            action();
        }
        catch (DirectoryNotFoundException)
        {
            WidgetManager.ScreenLayerManager.ShowMessage("Cannot find Global Assembly Cache folders. Consider your installation of .NET.");
        }
        catch (Exception e)
        {
            WidgetManager.ScreenLayerManager.ShowMessage($"An error occurred. Check your .NET installation.\nException:\n{e}");
        }
    }
}