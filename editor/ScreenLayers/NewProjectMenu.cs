namespace StorybrewEditor.ScreenLayers;

using System.IO;
using System.Linq;
using BrewLib.UserInterface;
using BrewLib.Util;
using Storyboarding;
using StorybrewEditor.Util;
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
                projectNameTextbox = new(WidgetManager) { LabelText = "Project Name", AnchorFrom = BoxAlignment.Centre },
                mapsetPathSelector = new(WidgetManager, PathSelectorMode.OpenDirectory)
                {
                    Value = OsuHelper.GetOsuSongFolder(),
                    LabelText = "Mapset Path",
                    AnchorFrom = BoxAlignment.Centre,
                    Filter = ".osu files (*.osu)|*.osu"
                },
                new LinearLayout(WidgetManager)
                {
                    Horizontal = true,
                    AnchorFrom = BoxAlignment.Centre,
                    Fill = true,
                    Children =
                    [
                        startButton = new(WidgetManager) { Text = "Start", AnchorFrom = BoxAlignment.Centre },
                        cancelButton = new(WidgetManager) { Text = "Cancel", AnchorFrom = BoxAlignment.Centre }
                    ]
                }
            ]
        });

        projectNameTextbox.OnValueChanged += (_, _) => updateButtonsState();
        projectNameTextbox.OnValueCommited += (_, _) => projectNameTextbox.Value = Path.GetInvalidFileNameChars()
            .Aggregate(projectNameTextbox.Value, (current, character) => current.Replace(character, '_'));

        mapsetPathSelector.OnValueChanged += (_, _) => updateButtonsState();
        mapsetPathSelector.OnValueCommited += (_, _) =>
        {
            if (!Directory.Exists(mapsetPathSelector.Value) && File.Exists(mapsetPathSelector.Value))
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
        () =>
        {
            var project = Project.Create(projectNameTextbox.Value,
                mapsetPathSelector.Value,
                true,
                Manager.GetContext<Editor>().ResourceContainer);

            Program.Schedule(() => Manager.Set(new ProjectMenu(project)));
        });

    void updateButtonsState() => startButton.Disabled = !updateFieldsValid();
    bool updateFieldsValid()
    {
        var projectFolderName = projectNameTextbox.Value;
        if (string.IsNullOrWhiteSpace(projectFolderName))
        {
            startButton.Tooltip = "The project name isn't valid";
            return false;
        }

        var projectFolderPath = Path.Combine(Project.ProjectsFolder, projectFolderName);
        if (Directory.Exists(projectFolderPath))
        {
            startButton.Tooltip = $"A project named '{projectFolderName}' already exists";
            return false;
        }

        if (!Directory.Exists(mapsetPathSelector.Value))
        {
            startButton.Tooltip = "The selected mapset folder does not exist";
            return false;
        }

        if (!Directory.EnumerateFiles(mapsetPathSelector.Value, "*.osu", SearchOption.TopDirectoryOnly).Any())
        {
            startButton.Tooltip = "No .osu found in the selected mapset folder";
            return false;
        }

        startButton.Tooltip = "";
        return true;
    }
}