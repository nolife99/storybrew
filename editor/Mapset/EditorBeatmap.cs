namespace StorybrewEditor.Mapset;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;

public class EditorBeatmap(string path) : Beatmap
{
    static readonly Color[] defaultComboColors =
    [
        Color.FromPixel(new Rgba32(255, 192, 0)),
        Color.FromPixel(new Rgba32(0, 202, 0)),
        Color.FromPixel(new Rgba32(18, 124, 255)),
        Color.FromPixel(new Rgba32(242, 24, 57))
    ];

    readonly List<int> bookmarks = [];

    readonly List<OsuBreak> breaks = [];
    readonly List<Color> comboColors = [..defaultComboColors];
    readonly List<OsuHitObject> hitObjects = [];

    float approachRate = 5, circleSize = 5, hpDrainRate = 5, overallDifficulty = 5, sliderMultiplier = 1.4f,
        sliderTickRate = 1, stackLeniency = .7f;

    string audioFilename = "audio.mp3", backgroundPath, name = "";
    bool hitObjectsPostProcessed;
    long id;
    public string Path => path;

    public override string AudioFilename => audioFilename;
    public override string Name => name;
    public override long Id => id;
    public override float StackLeniency => stackLeniency;
    public override ReadOnlySpan<int> Bookmarks => CollectionsMarshal.AsSpan(bookmarks);
    public override float HpDrainRate => hpDrainRate;
    public override float CircleSize => circleSize;
    public override float OverallDifficulty => overallDifficulty;
    public override float ApproachRate => approachRate;
    public override float SliderMultiplier => sliderMultiplier;
    public override float SliderTickRate => sliderTickRate;

    public override ReadOnlySpan<OsuHitObject> HitObjects
    {
        get
        {
            if (!hitObjectsPostProcessed) postProcessHitObjects();
            return CollectionsMarshal.AsSpan(hitObjects);
        }
    }

    public override ReadOnlySpan<Color> ComboColors => CollectionsMarshal.AsSpan(comboColors);
    public override string BackgroundPath => backgroundPath;
    public override ReadOnlySpan<OsuBreak> Breaks => CollectionsMarshal.AsSpan(breaks);

    public override string ToString() => Name;

    #region Timing

    readonly List<ControlPoint> controlPoints = [], timingPoints = [];

    public override ReadOnlySpan<ControlPoint> ControlPoints => CollectionsMarshal.AsSpan(controlPoints);

    public override ReadOnlySpan<ControlPoint> TimingPoints => CollectionsMarshal.AsSpan(timingPoints);

    public override ControlPoint GetControlPointAt(float time)
    {
        ControlPoint closestTimingPoint = null;
        foreach (var controlPoint in controlPoints)
            if (closestTimingPoint is null || controlPoint.Offset - time <= ControlPointLeniency)
                closestTimingPoint = controlPoint;
            else break;

        return closestTimingPoint ?? ControlPoint.Default;
    }

    public override ControlPoint GetTimingPointAt(float time)
    {
        ControlPoint closestTimingPoint = null;
        foreach (var controlPoint in timingPoints)
            if (closestTimingPoint is null || controlPoint.Offset - time <= ControlPointLeniency)
                closestTimingPoint = controlPoint;
            else break;

        return closestTimingPoint ?? ControlPoint.Default;
    }

    #endregion

    #region .osu parsing

    public static EditorBeatmap Load(string path)
    {
        Trace.WriteLine($"Loading beatmap {path}");
        try
        {
            EditorBeatmap beatmap = new(path);

            using var reader = File.OpenText(path);
            reader.ParseSections((section, state) =>
                {
                    switch (section)
                    {
                        case "General":
                            state.reader.ParseKeyValueSection((key, value, bm) =>
                                {
                                    switch (key)
                                    {
                                        case "AudioFilename": bm.audioFilename = value.ToString(); break;

                                        case "StackLeniency":
                                            bm.stackLeniency = float.Parse(value, CultureInfo.InvariantCulture); break;
                                    }
                                },
                                state.beatmap); break;

                        case "Editor":
                            state.reader.ParseKeyValueSection((key, value, bm) =>
                                {
                                    switch (key)
                                    {
                                        case "Bookmarks":
                                            foreach (var bookmark in value.Split(','))
                                                if (value.Length > 0)
                                                {
                                                    var time = int.Parse(value[bookmark], CultureInfo.InvariantCulture);
                                                    if (!bm.bookmarks.Contains(time))
                                                        bm.bookmarks.Add(int.Parse(value[bookmark],
                                                            CultureInfo.InvariantCulture));
                                                }

                                            break;
                                    }
                                },
                                state.beatmap); break;

                        case "Metadata":
                            state.reader.ParseKeyValueSection((key, value, bm) =>
                                {
                                    switch (key)
                                    {
                                        case "Version": bm.name = value.ToString(); break;

                                        case "BeatmapID": bm.id = long.Parse(value, CultureInfo.InvariantCulture); break;
                                    }
                                },
                                state.beatmap); break;

                        case "Difficulty":
                            state.reader.ParseKeyValueSection((key, value, bm) =>
                                {
                                    switch (key)
                                    {
                                        case "HPDrainRate":
                                            bm.hpDrainRate = float.Parse(value, CultureInfo.InvariantCulture); break;

                                        case "CircleSize":
                                            bm.circleSize = float.Parse(value, CultureInfo.InvariantCulture); break;

                                        case "OverallDifficulty":
                                            bm.overallDifficulty = float.Parse(value, CultureInfo.InvariantCulture); break;

                                        case "ApproachRate":
                                            bm.approachRate = float.Parse(value, CultureInfo.InvariantCulture); break;

                                        case "SliderMultiplier":
                                            bm.sliderMultiplier = float.Parse(value, CultureInfo.InvariantCulture); break;

                                        case "SliderTickRate":
                                            bm.sliderTickRate = float.Parse(value, CultureInfo.InvariantCulture); break;
                                    }
                                },
                                state.beatmap); break;

                        case "Events":
                            state.reader.ParseSectionLines((line, bm) =>
                                {
                                    if (line.StartsWith("//", StringComparison.Ordinal)) return;

                                    if (line.StartsWith(' ')) return;

                                    var values = line.Split(',');
                                    values.MoveNext();

                                    switch (line[values.Current])
                                    {
                                        case "0":
                                        {
                                            values.MoveNext();
                                            values.MoveNext();

                                            bm.backgroundPath = removePathQuotes(line[values.Current]);
                                            break;
                                        }

                                        case "2": bm.breaks.Add(OsuBreak.Parse(line)); break;
                                    }
                                },
                                state.beatmap,
                                false); break;

                        case "TimingPoints":
                        {
                            state.reader.ParseSectionLines((line, bm) => bm.controlPoints.Add(ControlPoint.Parse(line)),
                                beatmap);

                            beatmap.controlPoints.Sort();
                            foreach (var cp in beatmap.controlPoints)
                                if (!cp.IsInherited)
                                    beatmap.timingPoints.Add(cp);

                            break;
                        }

                        case "Colours":
                        {
                            beatmap.comboColors.Clear();
                            state.reader.ParseKeyValueSection((key, value, bm) =>
                                {
                                    if (!key.StartsWith("Combo", StringComparison.Ordinal)) return;

                                    var rgb = value.Split(',');
                                    rgb.MoveNext();

                                    var r = rgb.Current;
                                    rgb.MoveNext();

                                    var g = rgb.Current;
                                    rgb.MoveNext();

                                    var b = rgb.Current;

                                    bm.comboColors.Add(Color.FromPixel(new Rgba32(
                                        byte.Parse(value[r], CultureInfo.InvariantCulture),
                                        byte.Parse(value[g], CultureInfo.InvariantCulture),
                                        byte.Parse(value[b], CultureInfo.InvariantCulture))));
                                },
                                state.beatmap);

                            if (beatmap.comboColors.Count == 0) beatmap.comboColors.AddRange(defaultComboColors);

                            break;
                        }

                        case "HitObjects":
                        {
                            OsuHitObject previousHitObject = null;
                            var colorIndex = 0;
                            var comboIndex = 0;

                            state.reader.ParseSectionLines((line, bm) =>
                                {
                                    var hitobject = OsuHitObject.Parse(bm, line);

                                    if (hitobject.NewCombo ||
                                        previousHitObject is null ||
                                        (previousHitObject.Flags & HitObjectFlag.Spinner) > 0)
                                    {
                                        hitobject.Flags |= HitObjectFlag.NewCombo;

                                        var colorIncrement = hitobject.ComboOffset;
                                        if ((hitobject.Flags & HitObjectFlag.Spinner) == 0) ++colorIncrement;

                                        colorIndex = (colorIndex + colorIncrement) % bm.comboColors.Count;

                                        comboIndex = 1;
                                    }
                                    else ++comboIndex;

                                    hitobject.ComboIndex = comboIndex;
                                    hitobject.ColorIndex = colorIndex;
                                    hitobject.Color = bm.comboColors[colorIndex];

                                    bm.hitObjects.Add(hitobject);
                                    previousHitObject = hitobject;
                                },
                                state.beatmap,
                                false);

                            break;
                        }
                    }
                },
                (reader, beatmap));

            return beatmap;
        }
        catch (Exception e)
        {
            throw new BeatmapLoadingException(
                $"Failed to load beatmap \"{System.IO.Path.GetFileNameWithoutExtension(path)}\".",
                e);
        }
    }

    void postProcessHitObjects()
    {
        hitObjectsPostProcessed = true;

        const int stackLenienceSquared = 9;
        var preemtTime = GetDifficultyRange(ApproachRate, 1800, 1200, 450);

        for (var i = hitObjects.Count - 1; i > 0; --i)
        {
            var objectI = hitObjects[i];
            if (objectI.StackIndex != 0 || objectI is OsuSpinner) continue;

            var n = i;
            switch (objectI)
            {
                case OsuCircle:
                {
                    while (--n >= 0)
                    {
                        var objectN = hitObjects[n];
                        if (objectN is OsuSpinner) continue;

                        if (objectI.StartTime - preemtTime * StackLeniency > objectN.EndTime) break;

                        if (objectN is OsuSlider spanN &&
                            (spanN.PlayfieldEndPosition - objectI.PlayfieldPosition).LengthSquared() < stackLenienceSquared)
                        {
                            var offset = objectI.StackIndex - objectN.StackIndex + 1;

                            for (var j = n + 1; j <= i; ++j)
                                if ((spanN.PlayfieldEndPosition - hitObjects[j].PlayfieldPosition).LengthSquared() <
                                    stackLenienceSquared)
                                    hitObjects[j].StackIndex -= offset;

                            break;
                        }

                        if (!((objectN.PlayfieldPosition - objectI.PlayfieldPosition).LengthSquared() <
                            stackLenienceSquared)) continue;

                        objectN.StackIndex = objectI.StackIndex + 1;
                        objectI = objectN;
                    }

                    break;
                }

                case OsuSlider:
                {
                    while (--n >= 0)
                    {
                        var objectN = hitObjects[n];
                        if (objectN is OsuSpinner) continue;

                        if (objectI.StartTime - preemtTime * StackLeniency > objectN.StartTime) break;

                        if (!((((objectN as OsuSlider)?.PlayfieldEndPosition ?? objectN.PlayfieldPosition) -
                                objectI.PlayfieldPosition).LengthSquared() <
                            stackLenienceSquared)) continue;

                        objectN.StackIndex = objectI.StackIndex + 1;
                        objectI = objectN;
                    }

                    break;
                }
            }
        }

        var hitobjectScale = (1 - .7f * (CircleSize - 5) / 5) / 2;
        var hitObjectRadius = 64 * hitobjectScale;
        var stackOffset = hitObjectRadius / 10;

        foreach (var h in hitObjects) h.StackOffset = new CommandPosition(-stackOffset, -stackOffset) * h.StackIndex;
    }

    static string removePathQuotes(scoped ReadOnlySpan<char> path)
        => path.StartsWith('"') && path.EndsWith('"') ? path[1..^1].ToString() : path.ToString();

    #endregion
}