namespace StorybrewEditor.UserInterface.Components;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using BrewLib.IO;
using BrewLib.UserInterface;
using BrewLib.Util;
using ScreenLayers;
using Storyboarding;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Util;

public partial class EffectList : Widget
{
    readonly EffectConfigUi effectConfigUi;
    readonly LinearLayout layout, effectsLayout;
    Project project;

    public EffectList(WidgetManager manager, Project project, EffectConfigUi effectConfigUi) : base(manager)
    {
        this.project = project;
        this.effectConfigUi = effectConfigUi;

        Button addEffectButton, newScriptButton;
        Add(layout = new(manager)
        {
            StyleName = "panel",
            Padding = new(16),
            FitChildren = true,
            Fill = true,
            Children =
            [
                new Label(manager) { Text = "Effects", CanGrow = false },
                new ScrollArea(manager, effectsLayout = new(manager) { FitChildren = true }),
                new LinearLayout(manager)
                {
                    Fill = true,
                    FitChildren = true,
                    Horizontal = true,
                    CanGrow = false,
                    Children =
                    [
                        addEffectButton = new(Manager)
                        {
                            StyleName = "small",
                            Text = "Add effect",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre
                        },
                        newScriptButton = new(Manager)
                        {
                            StyleName = "small",
                            Text = "New script",
                            AnchorFrom = BoxAlignment.Centre,
                            AnchorTo = BoxAlignment.Centre
                        }
                    ]
                }
            ]
        });

        addEffectButton.OnClick += (_, _) => Manager.ScreenLayerManager.ShowContextMenu("Select an effect",
            name => project.AddScriptedEffect(name),
            project.GetEffectNames());

        newScriptButton.OnClick += (_, _)
            => Manager.ScreenLayerManager.ShowPrompt("Script name", name => createScript(name.ToString()));

        project.OnEffectsChanged += project_OnEffectsChanged;
        refreshEffects();
    }

    public override Vector2 MinSize => layout.MinSize;
    public override Vector2 MaxSize => layout.MaxSize;
    public override Vector2 PreferredSize => layout.PreferredSize;

    public event Action<Effect> OnEffectPreselect, OnEffectSelected;

    protected override void Dispose(bool disposing)
    {
        if (disposing) project.OnEffectsChanged -= project_OnEffectsChanged;
        project = null;
        base.Dispose(disposing);
    }

    protected override void Layout()
    {
        base.Layout();
        layout.Size = Size;
    }

    void project_OnEffectsChanged(object sender, EventArgs e) => refreshEffects();

    void refreshEffects()
    {
        effectsLayout.ClearWidgets();

        using var temp = TempList.Create(project.Effects);
        temp.Sort((a, b) => a.Name.CompareTo(b.Name, StringComparison.Ordinal));

        foreach (var effect in temp) effectsLayout.Add(createEffectWidget(effect));
    }

    LinearLayout createEffectWidget(Effect effect)
    {
        Label nameLabel, detailsLabel;
        Button renameButton, statusButton, configButton, editButton, removeButton;

        using var text = getEffectDetails(effect);

        LinearLayout effectWidget = new(Manager)
        {
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre,
            Horizontal = true,
            FitChildren = true,
            Fill = true,
            Children =
            [
                renameButton = new(Manager)
                {
                    StyleName = "icon",
                    Icon = IconFont.DriveFileRenameOutline,
                    Tooltip = "Rename",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false
                },
                new LinearLayout(Manager)
                {
                    StyleName = "condensed",
                    Children =
                    [
                        nameLabel = new(Manager)
                        {
                            StyleName = "listItem",
                            Text = effect.Name,
                            AnchorFrom = BoxAlignment.Left,
                            AnchorTo = BoxAlignment.Left
                        },
                        detailsLabel = new(Manager)
                        {
                            StyleName = "listItemSecondary",
                            Text = text.AsReadOnlySpan(),
                            AnchorFrom = BoxAlignment.Left,
                            AnchorTo = BoxAlignment.Left
                        }
                    ]
                },
                statusButton = new(Manager)
                {
                    StyleName = "icon",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false,
                    Displayed = false
                },
                configButton = new(Manager)
                {
                    StyleName = "icon",
                    Icon = IconFont.Tune,
                    Tooltip = "Configure",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false
                },
                editButton = new(Manager)
                {
                    StyleName = "icon",
                    Icon = IconFont.Edit,
                    Tooltip = "Edit script",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false,
                    Disabled = effect.Path is null
                },
                removeButton = new(Manager)
                {
                    StyleName = "icon",
                    Icon = IconFont.Delete,
                    Tooltip = "Remove",
                    AnchorFrom = BoxAlignment.Centre,
                    AnchorTo = BoxAlignment.Centre,
                    CanGrow = false
                }
            ]
        };

        updateStatusButton(statusButton, effect);

        EventHandler changedHandler;
        effect.OnChanged += changedHandler = (_, _) =>
        {
            nameLabel.Text = effect.Name;

            using var text = getEffectDetails(effect);
            detailsLabel.Text = text.AsReadOnlySpan();
            updateStatusButton(statusButton, effect);
        };

        effectWidget.OnHovered += (_, e) =>
        {
            effect.Highlight = e.Hovered;
            OnEffectPreselect?.Invoke(e.Hovered ? effect : null);
        };

        var handledClick = false;
        effectWidget.OnClickDown += (_, _) => handledClick = true;

        effectWidget.OnClickUp += (evt, _) =>
        {
            if (handledClick && (evt.RelatedTarget == effectWidget || evt.RelatedTarget.HasAncestor(effectWidget)))
                OnEffectSelected?.Invoke(effect);

            handledClick = false;
        };

        effectWidget.OnDisposed += (_, _) =>
        {
            effect.Highlight = false;
            effect.OnChanged -= changedHandler;
        };

        statusButton.OnClick += (_, _) =>
        {
            switch (effect.Status)
            {
                case EffectStatus.Loading:
                case EffectStatus.Configuring:
                case EffectStatus.Updating:
                    effect.CancelUpdate();
                    statusButton.Tooltip = "Cancelling";
                    statusButton.Disabled = true;
                    break;

                case EffectStatus.UpdateCanceled:
                    effect.Refresh();
                    statusButton.Tooltip = "Refreshing";
                    statusButton.Disabled = true;
                    break;

                default:
                {
                    using var sb = TempList.Create<char>();
                    sb.AddRange("Status: ".AsSpan());
                    sb.AddRangeEnum(effect.Status);

                    if (!effect.StatusMessage.IsWhiteSpace())
                    {
                        sb.AddRange("\n\n".AsSpan());
                        sb.AddRange(effect.StatusMessage);
                    }

                    Manager.ScreenLayerManager.ShowMessage(sb.AsReadOnlySpan());

                    break;
                }
            }
        };

        renameButton.OnClick += (_, _) =>
        {
            using var text = TempList.Create("Pick a new name for ".AsSpan());
            text.AddRange(effect.Name);

            Manager.ScreenLayerManager.ShowPrompt("Effect name",
                text.AsReadOnlySpan(),
                effect.Name,
                newName =>
                {
                    effect.Name = newName.ToString();
                    refreshEffects();
                });
        };

        editButton.OnClick += (_, _) => openEffectEditor(effect);
        configButton.OnClick += (_, _) =>
        {
            if (!effectConfigUi.Displayed || effectConfigUi.Effect != effect)
            {
                effectConfigUi.Effect = effect;
                effectConfigUi.Displayed = true;
            }
            else effectConfigUi.Displayed = false;
        };

        removeButton.OnClick += (_, _) =>
        {
            using var text = TempList.Create("Remove ".AsSpan());
            text.AddRange(effect.Name);
            text.Add('?');

            Manager.ScreenLayerManager.ShowMessage(text.AsReadOnlySpan(), () => project.Remove(effect), true);
        };

        return effectWidget;
    }

    static void updateStatusButton(Button button, Effect effect)
    {
        button.Disabled = effect.StatusMessage.IsWhiteSpace();

        using var tooltip = TempList.Create<char>();
        tooltip.AddRangeEnum(effect.Status);

        button.Tooltip = tooltip.AsReadOnlySpan();

        switch (effect.Status)
        {
            case EffectStatus.Loading:
            case EffectStatus.Configuring:
            case EffectStatus.Updating:
                button.Icon = IconFont.StopCircle;
                tooltip.AddRange(" (Cancel)".AsSpan());
                button.Tooltip = tooltip.AsReadOnlySpan();
                button.Disabled = false;
                break;

            case EffectStatus.UpdateCanceled:
                button.Icon = IconFont.Refresh;
                button.Tooltip = "Refresh";
                button.Disabled = false;
                break;

            case EffectStatus.ReloadPending:
                button.Icon = IconFont.LinkOff;
                button.Disabled = true;
                break;

            case EffectStatus.CompilationFailed:
            case EffectStatus.LoadingFailed:
            case EffectStatus.ExecutionFailed: button.Icon = IconFont.BugReport; break;

            case EffectStatus.Ready:
                button.Icon = IconFont.Eco;
                button.Tooltip = "Open log";
                break;
        }

        button.Displayed = effect.Status != EffectStatus.Ready || !button.Disabled;
    }

    void createScript(string name)
    {
        var resourceContainer = Manager.ScreenLayerManager.GetContext<Editor>().ResourceContainer;

        name = ZeroOrMoreDigitsPrefixRegex()
            .Replace(NotLetterNorNumberRegex()
                    .Replace(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(AlphabetRegex().Replace(name, " $1")), ""),
                "");

        if (name.Length == 0) name = "EffectScript";

        var path = Path.Combine(project.ScriptsPath, $"{name}.cs");
        var script = resourceContainer.GetString("scripttemplate.csx", ResourceSource.Embedded);

        script = script.Replace("%CLASSNAME%", name);

        if (File.Exists(path))
        {
            Manager.ScreenLayerManager.ShowMessage($"There is already a script named {name}");

            return;
        }

        File.WriteAllText(path, script);
        openEffectEditor(project.AddScriptedEffect(name));
    }

    void openEffectEditor(Effect effect)
    {
        var editorPath = Path.GetDirectoryName(Path.GetFullPath("."));

        var root = Path.GetPathRoot(effect.Path);
        var solutionFolder = Path.GetDirectoryName(effect.Path);
        while (solutionFolder != root)
        {
            if (solutionFolder == editorPath) break;
            if (Directory.EnumerateFiles(solutionFolder, "*.sln").Any()) break;

            solutionFolder = Directory.GetParent(solutionFolder).FullName;
        }

        if (solutionFolder == root) solutionFolder = Path.GetDirectoryName(effect.Path);

        List<string> paths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code",
                "bin",
                "code"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code",
                "bin",
                "code"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code Insiders",
                "bin",
                "code-insiders"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code Insiders",
                "bin",
                "code-insiders")
        ];

        foreach (var path in Environment.GetEnvironmentVariable("path").Split(';'))
            if (PathHelper.IsValidPath(path))
            {
                paths.Add(Path.Combine(path, "code"));
                paths.Add(Path.Combine(path, "code-insiders"));
            }
            else Trace.TraceWarning($"Invalid path in environment variables: {path}");

        var arguments = $"\"{solutionFolder}\" \"{effect.Path}\" -r";
        if (Program.Settings.VerboseVsCode) arguments += " --verbose";

        foreach (var path in paths)
            try
            {
                if (!File.Exists(path)) continue;

                Trace.WriteLine($"Opening vscode with \"{path} {arguments}\"");
                Process.Start(new ProcessStartInfo(path, arguments)
                    {
                        UseShellExecute = true,
                        WindowStyle = Program.Settings.VerboseVsCode ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
                    })
                    ?.Dispose();

                return;
            }
            catch (Exception e)
            {
                Trace.TraceWarning($"Could not open vscode:\n{e}");
            }

        Manager.ScreenLayerManager.ShowMessage(
            "Visual Studio Code could not be found, do you want to install it?\n(You may have to restart after installing)",
            () => NetHelper.OpenUrl("https://code.visualstudio.com/"),
            true);
    }

    static TempList<char> getEffectDetails(Effect effect)
    {
        var str = TempList.Create("using ".AsSpan());
        if (effect.EstimatedSize > 30720)
        {
            str.AddRange(effect.BaseName);
            str.AddRange(" (".AsSpan());
            str.AddRange(StringHelper.ToByteSize(effect.EstimatedSize).AsSpan());
            str.Add(')');
        }
        else str.AddRange(effect.BaseName);

        return str;
    }

    [GeneratedRegex(@"([A-Z])")] private static partial Regex AlphabetRegex();

    [GeneratedRegex(@"[^0-9a-zA-Z]")] private static partial Regex NotLetterNorNumberRegex();

    [GeneratedRegex(@"^[\d-]*")] private static partial Regex ZeroOrMoreDigitsPrefixRegex();
}