namespace StorybrewEditor.ScreenLayers;

using System;
using System.IO;
using System.Linq;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.UserInterface;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class NewProjectMenu : UiScreenLayer
{
    LinearLayout mainLayout;
    PathSelector mapsetPathSelector;
    Textbox projectNameTextbox;
    Button startButton, cancelButton;

    public override void Load()
    {
        base.Load();

        WidgetManager.Root.StyleName = "panel";
        WidgetManager.Root.Add(mainLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Padding = new(16),
            FitChildren = true,
            Children =
            [
                new Label(WidgetManager) { Text = "New Project", AnchorFrom = BoxAlignment.Centre },
                projectNameTextbox = new(WidgetManager)
                {
                    LabelText = "Project Name", AnchorFrom = BoxAlignment.Centre
                },
                mapsetPathSelector = new(WidgetManager, PathSelectorMode.OpenDirectory)
                {
                    Value = OsuHelper.GetOsuSongFolder(),
                    LabelText = "Mapset Path",
                    AnchorFrom = BoxAlignment.Centre,
                    Filter = [new(".osu files", "osu")]
                },
                new LinearLayout(WidgetManager)
                {
                    Horizontal = true,
                    AnchorFrom = BoxAlignment.Centre,
                    Fill = true,
                    Children =
                    [
                        startButton = new(WidgetManager)
                        {
                            Text = "Start", AnchorFrom = BoxAlignment.Centre
                        },
                        cancelButton = new(WidgetManager)
                        {
                            Text = "Cancel", AnchorFrom = BoxAlignment.Centre
                        }
                    ]
                }
            ]
        });

        projectNameTextbox.OnValueChanged += (_, _) => updateButtonsState();
        projectNameTextbox.OnValueCommited += (sender, _) =>
        {
            var textbox = (Textbox)sender;
            var invalidChars = Path.GetInvalidFileNameChars();

            using var charArray = TempArray.Create(textbox.Value);
            foreach (ref var c in charArray)
                if (invalidChars.Contains(c))
                    c = '_';

            textbox.Value = charArray.AsReadOnlySpan();
        };

        mapsetPathSelector.OnValueChanged += (_, _) => updateButtonsState();
        mapsetPathSelector.OnValueCommited += (_, _) =>
        {
            var mapsetPath = mapsetPathSelector.Value.ToString();
            if (!Directory.Exists(mapsetPath) && File.Exists(mapsetPath))
            {
                mapsetPathSelector.Value = Path.GetDirectoryName(mapsetPathSelector.Value);
                return;
            }

            updateButtonsState();
        };

        updateButtonsState();

        cancelButton.OnClick += (_, _) => Exit();
        startButton.OnClick += (_, _) => Manager.AsyncLoading("Creating project",
            async () => await Program.Schedule(s => s.Item2.Manager.Set(new ProjectMenu(s.Item1)),
                (await Project.Create(projectNameTextbox.Value.ToString(), mapsetPathSelector.Value.ToString(), true, Manager.GetContext<Editor>().ResourceContainer),
                    this)));
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(300);
    }

    void updateButtonsState()
    {
        var projectFolderName = Path.GetFileName(projectNameTextbox.Value);
        if (projectFolderName.IsWhiteSpace())
        {
            startButton.Tooltip = "The project name isn't valid";
            startButton.Disabled = true;

            return;
        }

        var projectFolderPath = Path.Join(Project.ProjectsFolder, projectFolderName);
        if (Directory.Exists(projectFolderPath))
        {
            startButton.Tooltip = $"A project named '{projectFolderName}' already exists";
            startButton.Disabled = true;

            return;
        }

        var mapsetPath = mapsetPathSelector.Value.ToString();
        if (!Directory.Exists(mapsetPath))
        {
            startButton.Tooltip = "The selected mapset folder does not exist";
            startButton.Disabled = true;

            return;
        }

        if (!Directory.EnumerateFiles(mapsetPath, "*.osu", SearchOption.TopDirectoryOnly).Any())
        {
            startButton.Tooltip = "No .osu found in the selected mapset folder";
            startButton.Disabled = true;

            return;
        }

        startButton.Tooltip = "";
        startButton.Disabled = false;
    }
}