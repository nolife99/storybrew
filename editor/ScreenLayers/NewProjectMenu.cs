namespace StorybrewEditor.ScreenLayers;

using System;
using System.IO;
using System.Linq;
using BrewLib.UserInterface;
using BrewLib.Util;
using Storyboarding;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using UserInterface;

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
        projectNameTextbox.OnValueCommited += (_, _) =>
        {
            var invalidChars = Path.GetInvalidFileNameChars();

            var charArray = TempArray<char>.Create(projectNameTextbox.Value);
            for (var i = 0; i < charArray.Length; i++)
                if (invalidChars.Contains(charArray[i]))
                    charArray[i] = '_';

            using TempArrayInternals<char> internals = new(charArray);
            projectNameTextbox.Value = internals.Array.AsSpan(0, internals.Length);
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

        startButton.OnClick += (_, _) => createProject();
        cancelButton.OnClick += (_, _) => Exit();
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);
        mainLayout.Pack(300);
    }

    void createProject() => Manager.AsyncLoading("Creating project",
        async () =>
        {
            var project = await Project.Create(projectNameTextbox.Value.ToString(),
                mapsetPathSelector.Value.ToString(),
                true,
                Manager.GetContext<Editor>().ResourceContainer);

            await Program.Schedule(() => Manager.Set(new ProjectMenu(project)));
        });

    void updateButtonsState() => startButton.Disabled = !updateFieldsValid();

    bool updateFieldsValid()
    {
        var projectFolderName = projectNameTextbox.Value;
        if (projectFolderName.IsWhiteSpace())
        {
            startButton.Tooltip = "The project name isn't valid";
            return false;
        }

        var projectFolderPath = Path.Combine(Project.ProjectsFolder, projectFolderName.ToString());
        if (Directory.Exists(projectFolderPath))
        {
            startButton.Tooltip = $"A project named '{projectFolderName}' already exists";
            return false;
        }

        var mapsetPath = mapsetPathSelector.Value.ToString();
        if (!Directory.Exists(mapsetPath))
        {
            startButton.Tooltip = "The selected mapset folder does not exist";
            return false;
        }

        if (!Directory.EnumerateFiles(mapsetPath, "*.osu", SearchOption.TopDirectoryOnly).Any())
        {
            startButton.Tooltip = "No .osu found in the selected mapset folder";
            return false;
        }

        startButton.Tooltip = "";
        return true;
    }
}