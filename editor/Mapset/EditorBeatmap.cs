﻿using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;
using StorybrewEditor.Storyboarding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace StorybrewEditor.Mapset;

public class EditorBeatmap(string path) : Beatmap
{
    public readonly string Path = path;

    public override string AudioFilename => audioFilename;
    string audioFilename = "audio.mp3";

    string name = string.Empty;
    public override string Name => name;

    long id;
    public override long Id => id;

    double stackLeniency = 0.7;
    public override double StackLeniency => stackLeniency;

    readonly HashSet<int> bookmarks = [];
    public override IEnumerable<int> Bookmarks => bookmarks;

    double hpDrainRate = 5;
    public override double HpDrainRate => hpDrainRate;

    double circleSize = 5;
    public override double CircleSize => circleSize;

    double overallDifficulty = 5;
    public override double OverallDifficulty => overallDifficulty;

    double approachRate = 5;
    public override double ApproachRate => approachRate;

    double sliderMultiplier = 1.4;
    public override double SliderMultiplier => sliderMultiplier;

    double sliderTickRate = 1;
    public override double SliderTickRate => sliderTickRate;

    bool hitObjectsPostProcessed;
    readonly List<OsuHitObject> hitObjects = [];
    public override IEnumerable<OsuHitObject> HitObjects
    {
        get
        {
            if (!hitObjectsPostProcessed) postProcessHitObjects();
            return hitObjects;
        }
    }

    static readonly Color[] defaultComboColors =
    [
        Color.FromArgb(255, 192, 0),
        Color.FromArgb(0, 202, 0),
        Color.FromArgb(18, 124, 255),
        Color.FromArgb(242, 24, 57)
    ];
    readonly List<Color> comboColors = new(defaultComboColors);
    public override IEnumerable<Color> ComboColors => comboColors;

    string backgroundPath;
    public override string BackgroundPath => backgroundPath;

    readonly List<OsuBreak> breaks = [];
    public override IEnumerable<OsuBreak> Breaks => breaks;

    public override string ToString() => Name;

    #region Timing

    readonly List<ControlPoint> controlPoints = [];

    public override IEnumerable<ControlPoint> ControlPoints => controlPoints;
    public override IEnumerable<ControlPoint> TimingPoints => controlPoints.Where(c => !c.IsInherited);

    public ControlPoint GetControlPointAt(int time, Func<ControlPoint, bool> predicate)
    {
        if (controlPoints is null) return null;

        ControlPoint closestTimingPoint = null;
        foreach (var controlPoint in CollectionsMarshal.AsSpan(controlPoints))
        {
            if (predicate is not null && !predicate(controlPoint)) continue;
            if (closestTimingPoint is null || controlPoint.Offset - time <= ControlPointLeniency) closestTimingPoint = controlPoint;
            else break;
        }
        return closestTimingPoint ?? ControlPoint.Default;
    }

    public override ControlPoint GetControlPointAt(int time) => GetControlPointAt(time, null);
    public override ControlPoint GetTimingPointAt(int time) => GetControlPointAt(time, cp => !cp.IsInherited);

    #endregion

    #region .osu parsing

    public static EditorBeatmap Load(string path)
    {
        Trace.WriteLine($"Loading beatmap {path}");
        try
        {
            EditorBeatmap beatmap = new(path);
            using (var reader = File.OpenText(path)) reader.ParseSections(section =>
            {
                switch (section)
                {
                    case "General": reader.ParseKeyValueSection((key, value) =>
                    {
                        switch (key)
                        {
                            case "AudioFilename": beatmap.audioFilename = value; break;
                            case "StackLeniency": beatmap.stackLeniency = double.Parse(value, CultureInfo.InvariantCulture); break;
                        }
                    }); 
                    break;

                    case "Editor": reader.ParseKeyValueSection((key, value) =>
                    {
                        switch (key)
                        {
                            case "Bookmarks": foreach (var bookmark in value.Split(',')) if (value.Length > 0) 
                                beatmap.bookmarks.Add(int.Parse(bookmark, CultureInfo.InvariantCulture));
                                break;
                        }
                    }); 
                    break;

                    case "Metadata": reader.ParseKeyValueSection((key, value) =>
                    {
                        switch (key)
                        {
                            case "Version": beatmap.name = value; break;
                            case "BeatmapID": beatmap.id = long.Parse(value, CultureInfo.InvariantCulture); break;
                        }
                    }); 
                    break;

                    case "Difficulty": reader.ParseKeyValueSection((key, value) =>
                    {
                        switch (key)
                        {
                            case "HPDrainRate": beatmap.hpDrainRate = double.Parse(value, CultureInfo.InvariantCulture); break;
                            case "CircleSize": beatmap.circleSize = double.Parse(value, CultureInfo.InvariantCulture); break;
                            case "OverallDifficulty": beatmap.overallDifficulty = double.Parse(value, CultureInfo.InvariantCulture); break;
                            case "ApproachRate": beatmap.approachRate = double.Parse(value, CultureInfo.InvariantCulture); break;
                            case "SliderMultiplier": beatmap.sliderMultiplier = double.Parse(value, CultureInfo.InvariantCulture); break;
                            case "SliderTickRate": beatmap.sliderTickRate = double.Parse(value, CultureInfo.InvariantCulture); break;
                        }
                    }); 
                    break;

                    case "Events": reader.ParseSectionLines(line =>
                    {
                        if (line.StartsWith("//", StringComparison.Ordinal)) return;
                        if (line.StartsWith(' ')) return;

                        var values = line.Split(',');
                        switch (values[0])
                        {
                            case "0": beatmap.backgroundPath = removePathQuotes(values[2]); break;
                            case "2": beatmap.breaks.Add(OsuBreak.Parse(line)); break;
                        }
                    }, false); 
                    break;

                    case "TimingPoints": 
                    {
                        reader.ParseSectionLines(line => beatmap.controlPoints.Add(ControlPoint.Parse(line)));
                        beatmap.controlPoints.Sort();
                        break;
                    }
                    case "Colours":
                    {
                        beatmap.comboColors.Clear();
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            if (!key.StartsWith("Combo", StringComparison.Ordinal)) return;

                            var rgb = value.Split(',');
                            beatmap.comboColors.Add(Color.FromArgb(byte.Parse(rgb[0], CultureInfo.InvariantCulture), byte.Parse(rgb[1], CultureInfo.InvariantCulture), byte.Parse(rgb[2], CultureInfo.InvariantCulture)));
                        });

                        if (beatmap.comboColors.Count == 0) beatmap.comboColors.AddRange(defaultComboColors);
                        break;
                    }
                    case "HitObjects":
                    {
                        OsuHitObject previousHitObject = null;
                        var colorIndex = 0;
                        var comboIndex = 0;

                        reader.ParseSectionLines(line =>
                        {
                            var hitobject = OsuHitObject.Parse(beatmap, line);

                            if (hitobject.NewCombo || previousHitObject is null || (previousHitObject.Flags & HitObjectFlag.Spinner) > 0)
                            {
                                hitobject.Flags |= HitObjectFlag.NewCombo;

                                var colorIncrement = hitobject.ComboOffset;
                                if ((hitobject.Flags & HitObjectFlag.Spinner) == 0) colorIncrement++;
                                colorIndex = (colorIndex + colorIncrement) % beatmap.comboColors.Count;
                                comboIndex = 1;
                            }
                            else ++comboIndex;

                            hitobject.ComboIndex = comboIndex;
                            hitobject.ColorIndex = colorIndex;
                            hitobject.Color = beatmap.comboColors[colorIndex];

                            beatmap.hitObjects.Add(hitobject);
                            previousHitObject = hitobject;
                        }, false);

                        break;
                    }
                }
            });
            return beatmap;
        }
        catch (Exception e)
        {
            throw new BeatmapLoadingException($"Failed to load beatmap \"{System.IO.Path.GetFileNameWithoutExtension(path)}\".", e);
        }
    }
    void postProcessHitObjects()
    {
        hitObjectsPostProcessed = true;

        var stackLenienceSquared = 9;
        var preemtTime = GetDifficultyRange(ApproachRate, 1800, 1200, 450);

        for (var i = hitObjects.Count - 1; i > 0; --i)
        {
            var objectI = hitObjects[i];

            if (objectI.StackIndex != 0 || objectI is OsuSpinner) continue;

            var n = i;
            if (objectI is OsuCircle)
            {
                while (--n >= 0)
                {
                    var objectN = hitObjects[n];
                    if (objectN is OsuSpinner) continue;
                    if (objectI.StartTime - preemtTime * StackLeniency > objectN.EndTime) break;

                    if (objectN is OsuSlider spanN && (spanN.PlayfieldEndPosition - objectI.PlayfieldPosition).LengthSquared < stackLenienceSquared)
                    {
                        var offset = objectI.StackIndex - objectN.StackIndex + 1;
                        for (var j = n + 1; j <= i; ++j)
                            if ((spanN.PlayfieldEndPosition - hitObjects[j].PlayfieldPosition).LengthSquared < stackLenienceSquared)
                            hitObjects[j].StackIndex -= offset;

                        break;
                    }

                    if ((objectN.PlayfieldPosition - objectI.PlayfieldPosition).LengthSquared < stackLenienceSquared)
                    {
                        objectN.StackIndex = objectI.StackIndex + 1;
                        objectI = objectN;
                    }
                }
            }
            else if (objectI is OsuSlider) while (--n >= 0)
            {
                var objectN = hitObjects[n];
                if (objectN is OsuSpinner) continue;

                if (objectI.StartTime - preemtTime * StackLeniency > objectN.StartTime) break;

                if ((((objectN as OsuSlider)?.PlayfieldEndPosition ?? objectN.PlayfieldPosition) - objectI.PlayfieldPosition).LengthSquared < stackLenienceSquared)
                {
                    objectN.StackIndex = objectI.StackIndex + 1;
                    objectI = objectN;
                }
            }
        }

        var hitobjectScale = (1 - .7 * (CircleSize - 5) / 5) / 2;
        var hitObjectRadius = 64 * hitobjectScale;
        var stackOffset = hitObjectRadius / 10;

        hitObjects.ForEach(h => h.StackOffset = new CommandPosition(-stackOffset, -stackOffset) * h.StackIndex);
    }

    static string removePathQuotes(string path) => path.StartsWith('"') && path.EndsWith('"') ? path[1..^1] : path;

    #endregion
}