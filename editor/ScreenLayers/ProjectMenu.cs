namespace StorybrewEditor.ScreenLayers;

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Time;
using BrewLib.UserInterface;
using BrewLib.Util;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Scripting;
using Storyboarding;
using StorybrewCommon.Mapset;
using StorybrewEditor.Util;
using UserInterface;
using UserInterface.Components;
using UserInterface.Drawables;

public class ProjectMenu(Project proj) : UiScreenLayer
{
    AudioStream audio;

    int defaultDiv = 4;

    EffectList effects;

    EffectConfigUi effectUI;
    LayerList layers;
    float? pendingSeek;
    SettingsMenu settings;
    Label statusIcon, statusMessage, warningsLabel;

    LinearLayout statusLayout, bottomLeftLayout, bottomRightLayout;
    DrawableContainer storyboardContainer, previewContainer;
    StoryboardDrawable storyboardDrawable, previewDrawable;
    Vector2 storyboardPosition;

    Button timeB, divisorB, audioTimeB, mapB, fitB, playB, projFolderB, mapFolderB, saveB, exportB, settingB, effectB, layerB;

    TimelineSlider timeline;
    TimeSourceExtender timeSource;

    public override void Load()
    {
        base.Load();
        refreshAudio();

        WidgetManager.Root.Add(storyboardContainer = new(WidgetManager)
        {
            Drawable = storyboardDrawable = new(proj) { UpdateFrameStats = true },
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.Centre,
            AnchorTo = BoxAlignment.Centre
        });

        WidgetManager.Root.Add(bottomLeftLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.BottomLeft,
            AnchorTo = BoxAlignment.BottomLeft,
            Padding = new(16, 8, 16, 16),
            Horizontal = true,
            Fill = true,
            Children =
            [
                timeB = new(WidgetManager)
                {
                    StyleName = "small",
                    AnchorFrom = BoxAlignment.Centre,
                    Text = "--:--:---",
                    Tooltip = "Current time\nCtrl-C to copy",
                    CanGrow = false
                },
                divisorB = new(WidgetManager)
                {
                    StyleName = "small",
                    Text = $"1/{defaultDiv}",
                    Tooltip = "Snap divisor",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                audioTimeB = new(WidgetManager)
                {
                    StyleName = "small",
                    Text = $"{timeSource.TimeFactor:P0}",
                    Tooltip = "Audio speed",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                timeline = new(WidgetManager, proj) { AnchorFrom = BoxAlignment.Centre, SnapDivisor = defaultDiv },
                mapB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.LowPriority,
                    Tooltip = "Change beatmap",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                fitB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.FitScreen,
                    Tooltip = "Fit/Fill",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false,
                    Checkable = true
                },
                playB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.PlayCircle,
                    Tooltip = "Play/Pause\nShortcut: Space/K",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                }
            ]
        });

        WidgetManager.Root.Add(bottomRightLayout = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.BottomRight,
            Padding = new(16, 16, 16, 8),
            Horizontal = true,
            Children =
            [
                effectB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.DynamicForm,
                    Tooltip = "Effects",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                layerB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.Layers,
                    Tooltip = "Layers",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                settingB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.Settings,
                    Tooltip = "Settings",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                projFolderB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.FolderOpen,
                    Tooltip = "Open project folder",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                mapFolderB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.FolderSpecial,
                    Tooltip = "Open mapset folder\n(Right click to change)",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                saveB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.Save,
                    Tooltip = "Save project\nShortcut: Ctrl-S",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                },
                exportB = new(WidgetManager)
                {
                    StyleName = "icon",
                    Icon = IconFont.IosShare,
                    Tooltip = "Export to .osb\n(Right click to export once for each diff)",
                    AnchorFrom = BoxAlignment.Centre,
                    CanGrow = false
                }
            ]
        });

        WidgetManager.Root.Add(effectUI = new(WidgetManager)
        {
            AnchorTarget = WidgetManager.Root,
            AnchorFrom = BoxAlignment.TopLeft,
            AnchorTo = BoxAlignment.TopLeft,
            Offset = new(16, 16),
            Displayed = false
        });

        effectUI.OnDisplayedChanged += (_, _) => resizeStoryboard();

        WidgetManager.Root.Add(effects = new(WidgetManager, proj, effectUI)
        {
            AnchorTarget = bottomRightLayout,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.TopRight,
            Offset = new(-16, 0)
        });

        effects.OnEffectPreselect += effect =>
        {
            if (effect is not null) timeline.Highlight(effect.StartTime, effect.EndTime);
            else timeline.ClearHighlight();
        };

        effects.OnEffectSelected += effect => timeline.Value = effect.StartTime * .001f;

        WidgetManager.Root.Add(layers = new(WidgetManager, proj.LayerManager)
        {
            AnchorTarget = bottomRightLayout,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.TopRight,
            Offset = new(-16, 0)
        });

        layers.OnLayerPreselect += layer =>
        {
            if (layer is not null) timeline.Highlight(layer.StartTime, layer.EndTime);
            else timeline.ClearHighlight();
        };

        layers.OnLayerSelected += layer => timeline.Value = layer.StartTime * .001f;

        WidgetManager.Root.Add(settings = new(WidgetManager, proj)
        {
            AnchorTarget = bottomRightLayout,
            AnchorFrom = BoxAlignment.BottomRight,
            AnchorTo = BoxAlignment.TopRight,
            Offset = new(-16, 0)
        });

        WidgetManager.Root.Add(statusLayout = new(WidgetManager)
        {
            StyleName = "tooltip",
            AnchorTarget = bottomLeftLayout,
            AnchorFrom = BoxAlignment.BottomLeft,
            AnchorTo = BoxAlignment.TopLeft,
            Offset = new(16, 0),
            Horizontal = true,
            Hoverable = false,
            Displayed = false,
            Children =
            [
                statusIcon = new(WidgetManager) { StyleName = "icon", AnchorFrom = BoxAlignment.Left, CanGrow = false },
                statusMessage = new(WidgetManager) { AnchorFrom = BoxAlignment.Left }
            ]
        });

        WidgetManager.Root.Add(warningsLabel = new(WidgetManager)
        {
            StyleName = "tooltip",
            AnchorTarget = timeline,
            AnchorFrom = BoxAlignment.BottomLeft,
            AnchorTo = BoxAlignment.TopLeft,
            Offset = new(0, -8)
        });

        WidgetManager.Root.Add(previewContainer = new(WidgetManager)
        {
            StyleName = "storyboardPreview",
            Drawable = previewDrawable = new(proj),
            AnchorTarget = timeline,
            AnchorFrom = BoxAlignment.Bottom,
            AnchorTo = BoxAlignment.Top,
            Hoverable = false,
            Displayed = false,
            Size = new(256, 144)
        });

        timeB.OnClick += (_, _) => Manager.ShowPrompt("Skip to...",
            value =>
            {
                if (float.TryParse(value, out var time)) timeline.Value = time * .001f;
            });

        resizeTimeline();
        timeline.OnValueChanged += (_, _) => pendingSeek = timeline.Value;
        timeline.OnValueCommited += (_, _) => timeline.Snap();
        timeline.OnHovered += (_, e) => previewContainer.Displayed = e.Hovered;

        mapB.OnClick += (_, _) =>
        {
            if (proj.MapsetManager.BeatmapCount > 2)
                Manager.ShowContextMenu("Select a beatmap",
                    map => proj.SelectBeatmap(map.Id, map.Name),
                    proj.MapsetManager.Beatmaps);
            else proj.SwitchMainBeatmap();
        };

        Program.Settings.FitStoryboard.Bind(fitB, resizeStoryboard);
        playB.OnClick += (_, _) => timeSource.Playing = !timeSource.Playing;

        divisorB.OnClick += (_, _) =>
        {
            ++defaultDiv;
            if (defaultDiv is 5 or 7) ++defaultDiv;
            if (defaultDiv == 9) defaultDiv = 12;
            if (defaultDiv == 13) defaultDiv = 16;
            if (defaultDiv > 16) defaultDiv = 1;
            timeline.SnapDivisor = defaultDiv;
            divisorB.Text = $"1/{defaultDiv}";
        };

        audioTimeB.OnClick += (_, e) =>
        {
            switch (e)
            {
                case MouseButton.Left:
                {
                    var speed = timeSource.TimeFactor;
                    if (speed > 1) speed = 2;
                    speed /= 2;
                    if (speed < .2) speed = 1;
                    timeSource.TimeFactor = speed;
                    break;
                }
                case MouseButton.Right:
                {
                    var speed = timeSource.TimeFactor;
                    if (speed < 1) speed = 1;
                    speed += speed >= 2 ? 1 : .5f;
                    if (speed > 8) speed = 1;
                    timeSource.TimeFactor = speed;
                    break;
                }
                case MouseButton.Middle: timeSource.TimeFactor = timeSource.TimeFactor == 8 ? 1 : 8; break;
            }

            audioTimeB.Text = $"{timeSource.TimeFactor:P0}";
        };

        MakeTabs([settingB, effectB, layerB], [settings, effects, layers]);
        projFolderB.OnClick += (_, _) =>
        {
            var path = Path.GetFullPath(proj.ProjectFolderPath);
            if (Directory.Exists(path)) PathHelper.OpenExplorer(path);
        };

        mapFolderB.OnClick += (_, e) =>
        {
            var path = Path.GetFullPath(proj.MapsetPath);
            if (e is MouseButton.Right || !Directory.Exists(path)) changeMapsetFolder();
            else PathHelper.OpenExplorer(path);
        };

        saveB.OnClick += (_, _) => saveProject();
        exportB.OnClick += (_, e) =>
        {
            if (e is MouseButton.Right) exportProjectAll();
            else exportProject();
        };

        proj.OnMapsetPathChanged += project_OnMapsetPathChanged;
        proj.OnEffectsContentChanged += project_OnEffectsContentChanged;
        proj.OnEffectsStatusChanged += project_OnEffectsStatusChanged;

        if (!proj.MapsetPathIsValid)
            Manager.ShowMessage($"The mapset folder cannot be found.\n{proj.MapsetPath}\n\nPlease select a new one.",
                changeMapsetFolder,
                true);
    }

    public override bool OnKeyDown(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Right:
                if (e.Control)
                {
                    var nextBookmark =
                        proj.MainBeatmap.Bookmarks.FirstOrDefault(bookmark => bookmark > float.Round(timeline.Value * 1000) + 50);

                    if (nextBookmark != 0) timeline.Value = nextBookmark * .001f;
                }
                else timeline.Scroll(e.Shift ? 4 : 1);

                return true;

            case Keys.Left:
                if (e.Control)
                {
                    var prevBookmark =
                        proj.MainBeatmap.Bookmarks.LastOrDefault(bookmark => bookmark < float.Round(timeline.Value * 1000) - 500);

                    if (prevBookmark != 0) timeline.Value = prevBookmark * .001f;
                }
                else timeline.Scroll(e.Shift ? -4 : -1);

                return true;
        }

        if (e.IsRepeat) return base.OnKeyDown(e);
        switch (e.Key)
        {
            case Keys.Space:
            case Keys.K:
                playB.Click();
                return true;
            case Keys.O:
                withSavePrompt(Manager.ShowOpenProject);
                return true;
            case Keys.S:
                if (e.Control)
                {
                    saveProject();
                    return true;
                }

                break;
            case Keys.C:
                if (e.Control)
                {
                    if (e.Shift)
                        ClipboardHelper.SetText(TimeSpan.FromSeconds(timeSource.Current)
                            .ToString(Program.Settings.TimeCopyFormat, CultureInfo.InvariantCulture));
                    else if (e.Alt) ClipboardHelper.SetText($"{storyboardPosition.X:###}, {storyboardPosition.Y:###}");
                    else ClipboardHelper.SetText((timeSource.Current * 1000).ToString("f0", CultureInfo.InvariantCulture));

                    return true;
                }

                break;
        }

        return base.OnKeyDown(e);
    }

    public override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);

        var bounds = storyboardContainer.Bounds;
        var scale = OsuHitObject.StoryboardSize.Height / bounds.Height;

        storyboardPosition = (WidgetManager.MousePosition - (Vector2)bounds.Location) * scale;
        storyboardPosition.X -= (bounds.Width * scale - OsuHitObject.StoryboardSize.Width) * .5f;
    }

    public override bool OnMouseWheel(MouseWheelEventArgs e)
    {
        var inputManager = Manager.GetContext<Editor>().InputManager;
        timeline.Scroll(-e.OffsetY * (inputManager.Shift ? 4 : 1));
        return true;
    }

    void changeMapsetFolder()
    {
        var initialDirectory = Path.GetFullPath(proj.MapsetPath);
        if (!Directory.Exists(initialDirectory)) initialDirectory = OsuHelper.GetOsuSongFolder();

        Manager.OpenFilePicker("",
            initialDirectory,
            [new(".osu files", "osu")],
            newPath =>
            {
                if (!Directory.Exists(newPath) && File.Exists(newPath)) proj.MapsetPath = Path.GetDirectoryName(newPath);
                else Manager.ShowMessage("Invalid mapset path.");
            });
    }

    void saveProject() => Manager.AsyncLoading("Saving", proj.Save);
    void exportProject() => Manager.AsyncLoading("Exporting", () => proj.ExportToOsb());
    void exportProjectAll() => Manager.AsyncLoading("Exporting",
        async () =>
        {
            var first = true;
            var mainBeatmap = proj.MainBeatmap;

            foreach (var map in proj.MapsetManager.Beatmaps.ToArray())
            {
                await Program.Schedule(() => proj.MainBeatmap = map);
                while (proj.EffectsStatus != EffectStatus.Ready)
                {
                    switch (proj.EffectsStatus)
                    {
                        case EffectStatus.CompilationFailed:
                        case EffectStatus.ExecutionFailed:
                        case EffectStatus.LoadingFailed:
                            throw new ScriptLoadingException($"An effect failed to execute ({proj.EffectsStatus
                            })\nCheck its log for the actual error.");
                    }

                    await Task.Yield();
                }

                await proj.ExportToOsb(first);
                first = false;
            }

            if (proj.MainBeatmap != mainBeatmap) await Program.Schedule(() => proj.MainBeatmap = mainBeatmap);
        });

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!pendingSeek.HasValue) return;
        timeSource.Seek(Nullable.GetValueRefOrDefaultRef(ref pendingSeek));
        pendingSeek = null;
    }

    public override void Update(bool isTop, bool isCovered)
    {
        base.Update(isTop, isCovered);

        timeSource.Update();
        var time = pendingSeek ?? timeSource.Current;

        mapB.Disabled = proj.MapsetManager.BeatmapCount < 2;
        playB.Icon = timeSource.Playing ? IconFont.PauseCircle : IconFont.PlayCircle;
        saveB.Disabled = !proj.Changed;
        exportB.Disabled = !proj.MapsetPathIsValid;
        audio.Volume = WidgetManager.Root.Opacity;

        if (timeSource.Playing)
        {
            if (timeline.RepeatStart != timeline.RepeatEnd && (time < timeline.RepeatStart - .005f || timeline.RepeatEnd < time))
                pendingSeek = time = timeline.RepeatStart;
            else if (timeSource.Current > timeline.MaxValue)
            {
                timeSource.Playing = false;
                pendingSeek = timeline.MaxValue;
            }
        }

        timeline.SetValueSilent(time);
        if (Manager.GetContext<Editor>().IsFixedRateUpdate)
        {
            timeB.Text = Manager.GetContext<Editor>().InputManager.Alt ?
                $"{storyboardPosition.X:f0}, {storyboardPosition.Y:f0}" :
                TimeSpan.FromSeconds(time).ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);

            warningsLabel.Text = buildWarningMessage();
            warningsLabel.Displayed = warningsLabel.Text.Length != 0;
            warningsLabel.Pack(600);
            warningsLabel.Pack();
        }

        if (timeSource.Playing && storyboardDrawable.Time < time) proj.TriggerEvents(storyboardDrawable.Time, time);

        storyboardDrawable.Time = time;
        storyboardDrawable.Clip = !Manager.GetContext<Editor>().InputManager.Alt;
        if (previewContainer.Visible)
            previewDrawable.Time = timeline.GetValueForPosition(Manager.GetContext<Editor>().InputManager.MousePosition);
    }
    string buildWarningMessage()
    {
        var warnings = StringHelper.StringBuilderPool.Retrieve();
        var stats = proj.FrameStats;

        var activeSprites = stats.SpriteCount;
        if (proj.DisplayDebugWarning && activeSprites < 1500)
            warnings.Append(CultureInfo.InvariantCulture, $"{activeSprites:n0} Sprites\n");
        else if (activeSprites >= 1500) warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {activeSprites:n0} Sprites\n");

        var batches = proj.FrameStats.Batches;
        if (proj.DisplayDebugWarning && batches < 500) warnings.Append(CultureInfo.InvariantCulture, $"{batches:0} Batches\n");
        else if (batches >= 500) warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {batches:0} Batches\n");

        var commands = stats.CommandCount;
        if (proj.DisplayDebugWarning && commands < 15000)
            warnings.Append(CultureInfo.InvariantCulture, $"{commands:n0} Commands\n");
        else if (commands >= 15000) warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {commands:n0} Commands\n");

        float activeCommands = stats.EffectiveCommandCount, unusedCommands = commands - activeCommands,
            unusedRatio = unusedCommands / Math.Max(1, commands);

        if (unusedCommands >= 5000 && unusedRatio > .5f ||
            unusedCommands >= 10000 && unusedRatio > .2f ||
            unusedCommands >= 15000)
            warnings.Append(CultureInfo.InvariantCulture,
                $"\ue002 {unusedCommands:n0} ({unusedRatio:0%}) Commands on Hidden Sprites\n");
        else if (proj.DisplayDebugWarning)
            warnings.Append(CultureInfo.InvariantCulture, $"{unusedCommands:n0} ({unusedRatio:0%}) Commands on Hidden Sprites\n");

        var sbLoad = stats.ScreenFill;
        switch (sbLoad)
        {
            case > 0 and < 5
                when proj.DisplayDebugWarning:
                warnings.Append(CultureInfo.InvariantCulture, $"{sbLoad:f2}x Screen Fill\n"); break;
            case >= 5: warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {sbLoad:f2}x Screen Fill\n"); break;
        }

        var frameGpuMemory = stats.GpuMemoryFrameMb;
        if (proj.DisplayDebugWarning && frameGpuMemory < 32)
            warnings.Append(CultureInfo.InvariantCulture, $"{frameGpuMemory:0.0}MB Frame Texture Memory\n");
        else if (frameGpuMemory >= 32)
            warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {frameGpuMemory:0.0}MB Frame Texture Memory\n");

        var totalGpuMemory = proj.TextureContainer.UncompressedMemoryUseMb;
        if (proj.DisplayDebugWarning && totalGpuMemory < 256)
            warnings.Append(CultureInfo.InvariantCulture, $"{totalGpuMemory:0.0}MB Total Texture Memory\n");
        else if (totalGpuMemory >= 256)
            warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {totalGpuMemory:0.0}MB Total Texture Memory\n");

        if (stats.OverlappedCommands) warnings.Append("\ue002 Overlapped Commands\n");
        if (stats.IncompatibleCommands) warnings.Append("\ue002 Incompatible Commands");

        var str = warnings.TrimEnd().ToString();
        StringHelper.StringBuilderPool.Release(warnings);
        return str;
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);

        var bottomRightWidth = 374;
        bottomRightLayout.Pack(bottomRightWidth / 1.6f);
        bottomLeftLayout.Pack(WidgetManager.Size.X - bottomRightWidth);

        settings.Pack(bottomRightWidth - 24, WidgetManager.Root.Height - bottomRightLayout.Height - 16);
        effects.Pack(bottomRightWidth - 24, WidgetManager.Root.Height - bottomRightLayout.Height - 16);
        layers.Pack(bottomRightWidth - 24, WidgetManager.Root.Height - bottomRightLayout.Height - 16);

        effectUI.Pack(bottomRightWidth, WidgetManager.Root.Height - bottomLeftLayout.Height - 16);
        resizeStoryboard();
    }

    void resizeStoryboard()
    {
        var parentSize = WidgetManager.Size;
        if (effectUI.Displayed)
        {
            storyboardContainer.Offset = new(effectUI.Bounds.Right / 2, 0);
            parentSize.X -= effectUI.Bounds.Right;
        }
        else storyboardContainer.Offset = Vector2.Zero;

        storyboardContainer.Size = fitB.Checked ? parentSize with { Y = parentSize.X * 9 / 16 } : parentSize;
    }

    void resizeTimeline()
    {
        timeline.MinValue = Math.Min(0, proj.StartTime * .001f);
        timeline.MaxValue = Math.Max(audio.Duration, proj.EndTime * .001f);
    }

    public override void Close() => withSavePrompt(() =>
    {
        proj.StopEffectUpdates();
        Manager.AsyncLoading("Stopping effect updates",
            async () =>
            {
                await proj.CancelEffectUpdates(true);
                await Program.Schedule(() => Manager.GetContext<Editor>().Restart());

                await Task.Delay(2000);
                GC.Collect();
            });
    });

    void withSavePrompt(Action action)
    {
        if (proj.Changed)
            Manager.ShowMessage("Do you wish to save the project?",
                () => Manager.AsyncLoading("Saving",
                    async () =>
                    {
                        await proj.Save();
                        await Program.Schedule(action);
                    }),
                action,
                true);
        else action();
    }

    void refreshAudio()
    {
        audio = Program.AudioManager.LoadStream(proj.AudioPath, Manager.GetContext<Editor>().ResourceContainer);
        timeSource = new(new AudioChannelTimeSource(audio));
    }

    void project_OnMapsetPathChanged(object sender, EventArgs e)
    {
        var previousAudio = audio;
        var previousTimeSource = timeSource;

        refreshAudio();
        resizeTimeline();

        if (previousAudio is null) return;

        pendingSeek = previousTimeSource.Current;
        timeSource.TimeFactor = previousTimeSource.TimeFactor;
        timeSource.Playing = previousTimeSource.Playing;
        previousAudio.Dispose();
    }

    void project_OnEffectsContentChanged(object sender, EventArgs e) => resizeTimeline();

    void project_OnEffectsStatusChanged(object sender, EventArgs e)
    {
        switch (proj.EffectsStatus)
        {
            case EffectStatus.ExecutionFailed:
                statusIcon.Icon = IconFont.BugReport;
                statusMessage.Text =
                    "An effect failed to execute.\nClick the Effects tab and the bug icon to see the error message.";

                statusLayout.Pack(1024 - bottomRightLayout.Width - 24);
                statusLayout.Displayed = true;
                break;

            case EffectStatus.Updating:
                statusIcon.Icon = IconFont.Sync;
                statusMessage.Text = "Updating effects...";
                statusLayout.Pack(1024 - bottomRightLayout.Width - 24);
                statusLayout.Displayed = true;
                break;

            default: statusLayout.Displayed = false; break;
        }
    }

    #region IDisposable Support

    bool disposed;
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposed) return;

        if (disposing)
        {
            proj.OnEffectsContentChanged -= project_OnEffectsContentChanged;
            proj.OnEffectsStatusChanged -= project_OnEffectsStatusChanged;
            proj.Dispose();
            audio.Dispose();
        }

        disposed = true;
    }

    #endregion
}