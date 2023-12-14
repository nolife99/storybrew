using System.Numerics;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewEditor.ScreenLayers;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.Util;

namespace StorybrewEditor.UserInterface.Components;

public class SettingsMenu : Widget
{
    readonly LinearLayout layout;

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public SettingsMenu(WidgetManager manager, Project project) : base(manager)
    {
        Button referencedAssemblyButton, floatingPointTimeButton, helpButton, displayWarningbutton, hitObjectsButton;
        Label dimLabel;
        Slider dimSlider;

        Add(layout = new(manager)
        {
            StyleName = "panel",
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children = new Widget[]
            {
                new Label(manager)
                {
                    Text = "Settings",
                    CanGrow = false
                },
                new LinearLayout(manager)
                {
                    Fill = true,
                    FitChildren = true,
                    CanGrow = false,
                    Children = new Widget[]
                    {
                        helpButton = new(manager)
                        {
                            Text = "Help!",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre
                        },
                        referencedAssemblyButton = new(manager)
                        {
                            Text = "View referenced assemblies",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre
                        },
                        new LinearLayout(manager)
                        {
                            StyleName = "condensed",
                            FitChildren = true,
                            Children = new Widget[]
                            {
                                dimLabel = new(manager)
                                {
                                    StyleName = "small",
                                    Text = "Dim"
                                },
                                dimSlider = new(manager)
                                {
                                    StyleName = "small",
                                    AnchorFrom = BoxAlignment.Centre,
                                    AnchorTo = BoxAlignment.Centre,
                                    Value = 0,
                                    Step = .05f
                                }
                            }
                        },
                        floatingPointTimeButton = new(manager)
                        {
                            Text = "Export time as floating-point",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre,
                            Checkable = true,
                            Checked = project.ExportSettings.UseFloatForTime,
                            Tooltip = "A storyboard exported with this option enabled\nwill only be compatible with lazer."
                        },
                        displayWarningbutton = new(manager)
                        {
                            Text = "Toggle debug warnings",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre,
                            Checkable = true,
                            Checked = project.DisplayDebugWarning,
                            Tooltip = "Display debug diagnostics about your storyboard."
                        },
                        hitObjectsButton = new(manager)
                        {
                            Text = "Toggle hit objects",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre,
                            Checkable = true,
                            Checked = project.ShowHitObjects,
                            Tooltip = "Displays hit objects of the current beatmap on\nthe timeline."
                        }
                    }
                }
            }
        });

        helpButton.OnClick += (sender, e) => NetHelper.OpenUrl($"https://github.com/{Program.Repository}/wiki");
        referencedAssemblyButton.OnClick += (sender, e) => Manager.ScreenLayerManager.Add(new ReferencedAssemblyConfig(project));
        dimSlider.OnValueChanged += (sender, e) =>
        {
            project.DimFactor = dimSlider.Value;
            dimLabel.Text = $"Dim ({project.DimFactor:p})";
        };
        floatingPointTimeButton.OnValueChanged += (sender, e) => project.ExportSettings.UseFloatForTime = floatingPointTimeButton.Checked;
        displayWarningbutton.OnValueChanged += (sender, e) => project.DisplayDebugWarning = displayWarningbutton.Checked;
        hitObjectsButton.OnValueChanged += (sender, e) => project.ShowHitObjects = hitObjectsButton.Checked;
    }

    protected override void Dispose(bool disposing) => base.Dispose(disposing);
    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }
}