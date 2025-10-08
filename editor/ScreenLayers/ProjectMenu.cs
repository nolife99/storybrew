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
using SDL3;
using StorybrewCommon.Mapset;
using StorybrewEditor.Scripting;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.UserInterface;
using StorybrewEditor.UserInterface.Components;
using StorybrewEditor.UserInterface.Drawables;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;

public class ProjectMenu(Project proj) : UiScreenLayer
{
    AudioStream audio;

    int defaultDiv = 4;

    EffectList effects;

    EffectConfigUi effectUI;
    LayerList layers;
    TimeSpan? pendingSeek;
    SettingsMenu settings;
    Label statusIcon, statusMessage, warningsLabel;

    LinearLayout statusLayout, bottomLeftLayout, bottomRightLayout;
    DrawableContainer storyboardContainer, previewContainer;
    StoryboardDrawable storyboardDrawable, previewDrawable;
    Vector2 storyboardPosition;

    Button timeB, divisorB, audioTimeB, mapB, fitB, playB, projFolderB, mapFolderB, saveB, exportB, settingB, effectB,
        layerB;

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
                timeline = new(WidgetManager, proj)
                {
                    AnchorFrom = BoxAlignment.Centre, SnapDivisor = defaultDiv
                },
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

        effects.OnEffectSelected += effect => timeline.Value = effect.StartTime * .001;

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

        layers.OnLayerSelected += layer => timeline.Value = layer.StartTime * .001;

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
                statusIcon = new(WidgetManager)
                {
                    StyleName = "icon", AnchorFrom = BoxAlignment.Left, CanGrow = false
                },
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
                if (float.TryParse(value, out var time)) timeline.Value = time * .001;
            });

        resizeTimeline();
        timeline.OnValueChanged += (_, _) => pendingSeek = TimeSpan.FromSeconds(timeline.Value);
        timeline.OnValueCommited += (sender, _) => ((TimelineSlider)sender).Snap();
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
                case SDL.ButtonLeft:
                {
                    var speed = timeSource.TimeFactor;
                    if (speed > 1) speed = 2;
                    speed /= 2;
                    if (speed < .2) speed = 1;
                    timeSource.TimeFactor = speed;
                    break;
                }

                case SDL.ButtonRight:
                {
                    var speed = timeSource.TimeFactor;
                    if (speed < 1) speed = 1;
                    speed += speed >= 2 ? 1 : .5f;
                    if (speed > 8) speed = 1;
                    timeSource.TimeFactor = speed;
                    break;
                }

                case SDL.ButtonMiddle: timeSource.TimeFactor = timeSource.TimeFactor == 8 ? 1 : 8; break;
            }

            using var str = StringHelper.Interpolate($"{timeSource.TimeFactor:P0}");
            audioTimeB.Text = str.AsReadOnlySpan();
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
            if (e == SDL.ButtonRight || !Directory.Exists(path)) changeMapsetFolder();
            else PathHelper.OpenExplorer(path);
        };

        saveB.OnClick += (_, _) => saveProject();
        exportB.OnClick += (_, e) =>
        {
            if (e == SDL.ButtonRight) exportProjectAll();
            else exportProject();
        };

        proj.LayerManager.OnLayersChanged += (_, _) =>
        {
            using var bytes = StringHelper.ToByteSize(proj.LayerManager.Layers.Sum(l => l.EstimatedSize));
            using var text = StringHelper.Interpolate(
                $"Export to .osb ({bytes.AsReadOnlySpan()})\n(Right click to export once for each diff)");

            exportB.Tooltip = text.AsReadOnlySpan();
        };

        proj.OnMapsetPathChanged += project_OnMapsetPathChanged;
        proj.OnEffectsContentChanged += project_OnEffectsContentChanged;
        proj.OnEffectsStatusChanged += project_OnEffectsStatusChanged;

        if (!proj.MapsetPathIsValid)
        {
            using var text = StringHelper.Interpolate(
                $"The mapset folder cannot be found.\n{proj.MapsetPath}\n\nPlease select a new one.");

            Manager.ShowMessage(text.AsReadOnlySpan(), changeMapsetFolder, true);
        }
        else project_OnEffectsStatusChanged(proj, EventArgs.Empty);
    }

    public override bool OnKeyDown(KeyboardEvent e)
    {
        switch (e.Key)
        {
            case Keycode.Right:
                if ((e.Mod & SDL.Keymod.Ctrl) != 0)
                {
                    foreach (var bookmark in proj.MainBeatmap.Bookmarks)
                        if (bookmark > double.Round(timeline.Value * 1000) + 50)
                        {
                            timeline.Value = bookmark * .001f;
                            break;
                        }
                }
                else timeline.Scroll((e.Mod & SDL.Keymod.Shift) != 0 ? 4 : 1);

                return true;

            case Keycode.Left:
                if ((e.Mod & SDL.Keymod.Ctrl) != 0)
                    for (var i = proj.MainBeatmap.Bookmarks.Length - 1; i >= 0; --i)
                    {
                        var bookmark = proj.MainBeatmap.Bookmarks[i];
                        if (!(bookmark < double.Round(timeline.Value * 1000) - 500)) continue;

                        timeline.Value = bookmark * .001f;
                        break;
                    }
                else timeline.Scroll((e.Mod & SDL.Keymod.Shift) != 0 ? -4 : -1);

                return true;
        }

        if (e.Repeat) return base.OnKeyDown(e);

        switch (e.Key)
        {
            case Keycode.Space:
            case Keycode.KpSpace:
            case Keycode.K:
                playB.Click();
                return true;

            case Keycode.O:
                withSavePrompt(Manager.ShowOpenProject);
                return true;

            case Keycode.S:
                if ((e.Mod & SDL.Keymod.Ctrl) != 0)
                {
                    saveProject();
                    return true;
                }

                break;

            case Keycode.C:
                if ((e.Mod & SDL.Keymod.Ctrl) != 0)
                {
                    if ((e.Mod & SDL.Keymod.Shift) != 0)
                        ClipboardHelper.SetText(timeSource.Current.ToString(Program.Settings.TimeCopyFormat,
                            CultureInfo.InvariantCulture));
                    else if ((e.Mod & SDL.Keymod.Alt) != 0)
                    {
                        using var str = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                            $"{storyboardPosition.X:###}, {storyboardPosition.Y:###}");

                        ClipboardHelper.SetText(str.AsReadOnlySpan());
                    }
                    else
                    {
                        using var str = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                            $"{timeSource.Current.TotalMilliseconds:f0}");

                        ClipboardHelper.SetText(
                            timeSource.Current.TotalMilliseconds.ToString("f0", CultureInfo.InvariantCulture));
                    }

                    return true;
                }

                break;
        }

        return base.OnKeyDown(e);
    }

    public override void OnMouseMove(SDL.MouseMotionEvent e)
    {
        base.OnMouseMove(e);

        var bounds = storyboardContainer.Bounds;
        var scale = OsuHitObject.StoryboardSize.Height / bounds.Height;

        storyboardPosition = (WidgetManager.MousePosition - (Vector2)bounds.Location) * scale;
        storyboardPosition.X -= (bounds.Width * scale - OsuHitObject.StoryboardSize.Width) * .5f;
    }

    public override bool OnMouseWheel(SDL.MouseWheelEvent e)
    {
        var inputManager = Manager.GetContext<Editor>().InputManager;
        timeline.Scroll(-e.Y * (inputManager.Shift ? 4 : 1));
        return true;
    }

    void changeMapsetFolder()
    {
        var initialDirectory = Path.GetFullPath(proj.MapsetPath);
        if (!Directory.Exists(initialDirectory)) initialDirectory = OsuHelper.GetOsuSongFolder();

        Manager.OpenFilePicker(default,
            initialDirectory,
            [new(".osu files (.osu)", "osu")],
            newPath =>
            {
                if (!Directory.Exists(newPath) && File.Exists(newPath))
                    proj.MapsetPath = Path.GetDirectoryName(newPath);
                else Manager.ShowMessage("Invalid mapset path.");
            });
    }

    void saveProject() => Manager.AsyncLoading("Saving", proj.Save);
    void exportProject() => Manager.AsyncLoading("Exporting", () => proj.ExportToOsb());

    void exportProjectAll()
        => Manager.AsyncLoading("Exporting",
            async () =>
            {
                var first = true;
                var mainBeatmap = proj.MainBeatmap;

                using (var array = ValueArray.Create(proj.MapsetManager.Beatmaps))
                    foreach (var map in array)
                    {
                        await Program.Schedule(s => s.proj.MainBeatmap = s.map, (map, proj));
                        while (proj.EffectsStatus is not EffectStatus.Ready)
                        {
                            switch (proj.EffectsStatus)
                            {
                                case EffectStatus.CompilationFailed:
                                case EffectStatus.ExecutionFailed:
                                case EffectStatus.LoadingFailed:
                                    throw new ScriptLoadingException(
                                        $"An effect failed to execute ({proj.EffectsStatus})\nCheck its log for the actual error.");
                            }

                            await Task.Delay(100);
                        }

                        await proj.ExportToOsb(first);
                        first = false;
                    }

                if (proj.MainBeatmap != mainBeatmap)
                    await Program.Schedule(s => s.proj.MainBeatmap = s.mainBeatmap, (mainBeatmap, proj));
            });

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!pendingSeek.HasValue) return;

        timeSource.Seek(Nullable.GetValueRefOrDefaultRef(ref pendingSeek));
        pendingSeek = null;
    }

    public override void Update(bool isTopFocus, bool isCovered)
    {
        base.Update(isTopFocus, isCovered);

        timeSource.Update();
        var time = pendingSeek ?? timeSource.Current;

        mapB.Disabled = proj.MapsetManager.BeatmapCount < 2;
        playB.Icon = timeSource.Playing ? IconFont.PauseCircle : IconFont.PlayCircle;
        saveB.Disabled = !proj.Changed;
        exportB.Disabled = !proj.MapsetPathIsValid ||
            proj.EffectsStatus is not EffectStatus.Ready and not EffectStatus.UpdateCanceled;

        audio.Volume = WidgetManager.Root.Opacity;

        if (timeSource.Playing)
        {
            if (timeline.RepeatStart != timeline.RepeatEnd &&
                (time < TimeSpan.FromSeconds(timeline.RepeatStart) - TimeSpan.FromMilliseconds(5) ||
                    TimeSpan.FromSeconds(timeline.RepeatEnd) < time))
                pendingSeek = time = TimeSpan.FromSeconds(timeline.RepeatStart);
            else if (timeSource.Current > TimeSpan.FromSeconds(timeline.MaxValue))
            {
                timeSource.Playing = false;
                pendingSeek = TimeSpan.FromSeconds(timeline.MaxValue);
            }
        }

        timeline.SetValueSilent(time.TotalSeconds);
        if (Manager.GetContext<Editor>().IsFixedRateUpdate)
        {
            using (var temp = TempList.Create<char>())
            {
                if (Manager.GetContext<Editor>().InputManager.Alt)
                    temp.Append($"{storyboardPosition.X:f0}, {storyboardPosition.Y:f0}");
                else temp.Append($@"{time:mm\:ss\.fff}");

                timeB.Text = temp.AsReadOnlySpan();
            }

            using (var text = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                $"Current time ({time.TotalMilliseconds:f0})\nCtrl-C to copy")) timeB.Tooltip = text.AsReadOnlySpan();

            using (var text = buildWarningMessage()) warningsLabel.Text = text.AsReadOnlySpan();

            if (warningsLabel.NeedsLayout)
            {
                warningsLabel.Pack(650);
                warningsLabel.Pack();
            }

            warningsLabel.Displayed = warningsLabel.Text.Length != 0;
        }

        if (timeSource.Playing && storyboardDrawable.Time < time) proj.TriggerEvents(storyboardDrawable.Time, time);

        storyboardDrawable.Time = time;
        storyboardDrawable.Clip = !Manager.GetContext<Editor>().InputManager.Alt;
        if (previewContainer.Visible)
            previewDrawable.Time = TimeSpan.FromSeconds(
                timeline.GetValueForPosition(Manager.GetContext<Editor>().InputManager.MousePosition));
    }

    TempList<char> buildWarningMessage()
    {
        var warnings = TempList.Create<char>(1024);
        var stats = proj.FrameStats;

        var activeSprites = stats.SpriteCount;
        var prolongedSprites = stats.ProlongedSprites.Count;

        if (activeSprites >= 1500 || prolongedSprites != 0)
        {
            warnings.Append(CultureInfo.InvariantCulture, $"\ue002 {activeSprites:n0} Sprite");
            AppendPlural(ref warnings, activeSprites);

            if (prolongedSprites != 0)
            {
                warnings.Append(CultureInfo.InvariantCulture, $" ({prolongedSprites:n0} Prolonged Sprite");
                AppendPlural(ref warnings, prolongedSprites);

                if (proj.DisplayDebugWarning)
                {
                    warnings.AddRange(" (");
                    for (var i = 0; i < stats.ProlongedSprites.Count; ++i)
                    {
                        if (i != 0) warnings.AddRange(", ");
                        warnings.AddRange(stats.ProlongedSprites[i].TexturePath);
                    }

                    warnings.Add(')');
                }

                warnings.Add(')');
            }

            warnings.Add('\n');
        }
        else if (proj.DisplayDebugWarning && activeSprites > 0)
        {
            warnings.Append(CultureInfo.InvariantCulture, $"{activeSprites:n0} Sprite");
            AppendPlural(ref warnings, activeSprites);
            warnings.Add('\n');
        }

        int commands = stats.CommandCount, activeCommands = stats.EffectiveCommandCount,
            unusedCommands = commands - activeCommands;

        var unusedRatio = unusedCommands / float.Max(1, commands);

        var hiddenCommands = unusedCommands >= 5000 && unusedRatio > .5f ||
            unusedCommands >= 10000 && unusedRatio > .2f || unusedCommands >= 15000;

        var showWarning = commands >= 15000 || hiddenCommands;
        if (showWarning || proj.DisplayDebugWarning && commands > 0)
        {
            if (showWarning) warnings.AddRange("\ue002 ");

            warnings.Append(CultureInfo.InvariantCulture, $"{commands:n0} Command");
            AppendPlural(ref warnings, commands);

            if (unusedCommands > 0)
            {
                warnings.Append(CultureInfo.InvariantCulture, $" ({unusedCommands:n0} ({unusedRatio:0%}) Command");
                AppendPlural(ref warnings, unusedCommands);
                warnings.AddRange(" on Hidden Sprites)");
            }

            warnings.Add('\n');
        }

        if (stats.OverlappedSprites.Count != 0)
        {
            warnings.AddRange("\ue002 Overlapped Commands");
            if (proj.DisplayDebugWarning)
            {
                warnings.AddRange(" (");
                for (var i = 0; i < stats.OverlappedSprites.Count; ++i)
                {
                    if (i != 0) warnings.AddRange(", ");
                    warnings.AddRange(stats.OverlappedSprites[i].TexturePath);
                }

                warnings.Add(')');
            }

            warnings.Add('\n');
        }

        if (stats.IncompatibleSprites.Count != 0)
        {
            warnings.AddRange("\ue002 Incompatible Commands");
            if (proj.DisplayDebugWarning)
            {
                warnings.AddRange(" (");
                for (var i = 0; i < stats.IncompatibleSprites.Count; ++i)
                {
                    if (i != 0) warnings.AddRange(", ");
                    warnings.AddRange(stats.IncompatibleSprites[i].TexturePath);
                }

                warnings.Add(')');
            }

            warnings.Add('\n');
        }

        var screenFill = stats.ScreenFill;
        if (screenFill >= 5 || proj.DisplayDebugWarning && screenFill > 0)
        {
            if (screenFill >= 5) warnings.AddRange("\ue002 ");
            warnings.Append(CultureInfo.InvariantCulture, $"{screenFill:f2}x Screen Fill\n");
        }

        var batches = proj.FrameStats.Batches;
        if (batches >= 500 || proj.DisplayDebugWarning && batches > 0)
        {
            if (batches >= 500) warnings.AddRange("\ue002 ");
            warnings.Append(CultureInfo.InvariantCulture, $"{batches:n0} Batch");
            AppendPlural(ref warnings, batches, "es");
            warnings.Add('\n');
        }

        var frameGpuMemory = stats.GpuPixelsFrame * 4;
        var totalGpuMemory = proj.TextureContainer.UncompressedMemoryUse;

        var showMemoryWarning = frameGpuMemory >= 32000000 || totalGpuMemory >= 256000000;
        if (showMemoryWarning || proj.DisplayDebugWarning && (frameGpuMemory > 0 || totalGpuMemory > 0))
        {
            if (showMemoryWarning) warnings.AddRange("\ue002 ");
            if (frameGpuMemory > 0)
            {
                using var bytes = StringHelper.ToByteSize(frameGpuMemory);
                bytes.Remove(' ');

                warnings.Append(CultureInfo.InvariantCulture, $"{bytes.AsReadOnlySpan()} Frame Texture Memory");
                if (totalGpuMemory > 0) warnings.AddRange(" (");
            }

            if (totalGpuMemory > 0)
            {
                using var bytes = StringHelper.ToByteSize(totalGpuMemory);
                bytes.Remove(' ');

                warnings.Append(CultureInfo.InvariantCulture, $"{bytes.AsReadOnlySpan()} Total Texture Memory");
                if (frameGpuMemory > 0) warnings.Add(')');
            }

            warnings.Add('\n');
        }

        var trim = warnings.Count - warnings.AsReadOnlySpan().TrimEnd().Length;
        warnings.RemoveRange(warnings.Count - trim, trim);

        return warnings;

        static void AppendPlural(scoped ref TempList<char> builder, int count, string plural = "s")
        {
            if (count != 1) builder.AddRange(plural);
        }
    }

    public override void Resize(int width, int height)
    {
        base.Resize(width, height);

        const int bottomRightWidth = 374;
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

        storyboardContainer.Size = fitB.Checked ? new(parentSize.X, parentSize.X * 9 / 16) : parentSize;
    }

    void resizeTimeline()
    {
        timeline.MinValue = double.Min(0, proj.StartTime * .001);
        timeline.MaxValue = double.Max(audio.Duration.TotalSeconds, proj.EndTime * .001);
    }

    public override void Close()
        => withSavePrompt(() =>
        {
            proj.StopEffectUpdates();
            Manager.AsyncLoading("Stopping effect updates",
                async () =>
                {
                    await proj.CancelEffectUpdates(true);
                    await Program.Schedule(m => m.GetContext<Editor>().Restart(), Manager);

                    await Task.Delay(5000);

                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false, true);
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
                        await Program.Schedule(a => a(), action);
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

        proj.OnEffectsContentChanged -= project_OnEffectsContentChanged;
        proj.OnEffectsStatusChanged -= project_OnEffectsStatusChanged;

        if (disposing)
        {
            proj.Dispose();
            audio.Dispose();
        }

        disposed = true;
    }

    #endregion
}