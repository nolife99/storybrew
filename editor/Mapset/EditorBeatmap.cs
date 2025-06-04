namespace StorybrewEditor.Mapset;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StorybrewCommon.Mapset;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;

public class EditorBeatmap(string path) : Beatmap
{
    static readonly Color[] defaultComboColors =
    [
        Color.FromPixel(new Rgba32(255, 192, 0)),
        Color.FromPixel(new Rgba32(0, 202, 0)),
        Color.FromPixel(new Rgba32(18, 124, 255)),
        Color.FromPixel(new Rgba32(242, 24, 57))
    ];

    readonly PooledList<int> bookmarks = [];

    readonly PooledList<OsuBreak> breaks = [];
    readonly PooledList<Color> comboColors = [..defaultComboColors];
    readonly PooledList<OsuHitObject> hitObjects = [];
    public readonly string Path = path;

    float approachRate = 5;
    string audioFilename = "audio.mp3";

    string backgroundPath;

    float circleSize = 5;

    bool hitObjectsPostProcessed;

    float hpDrainRate = 5;

    long id;

    string name = "";

    float overallDifficulty = 5;

    float sliderMultiplier = 1.4f;

    float sliderTickRate = 1;

    float stackLeniency = .7f;

    public override string AudioFilename => audioFilename;
    public override string Name => name;
    public override long Id => id;
    public override float StackLeniency => stackLeniency;
    public override ReadOnlySpan<int> Bookmarks => bookmarks.AsReadOnlySpan();
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
            return hitObjects.AsReadOnlySpan();
        }
    }

    public override ReadOnlySpan<Color> ComboColors => comboColors.AsReadOnlySpan();
    public override string BackgroundPath => backgroundPath;
    public override ReadOnlySpan<OsuBreak> Breaks => breaks.AsReadOnlySpan();

    public override string ToString() => Name;

    #region Timing

    readonly PooledList<ControlPoint> controlPoints = [], timingPoints = [];

    public override ReadOnlySpan<ControlPoint> ControlPoints => controlPoints.AsReadOnlySpan();

    public override ReadOnlySpan<ControlPoint> TimingPoints => timingPoints.AsReadOnlySpan();

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
            reader.ParseSections(section =>
            {
                switch (section)
                {
                    case "General":
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            switch (key)
                            {
                                case "AudioFilename": beatmap.audioFilename = value.ToString(); break;

                                case "StackLeniency":
                                    beatmap.stackLeniency = float.Parse(value, CultureInfo.InvariantCulture); break;
                            }
                        }); break;

                    case "Editor":
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            switch (key)
                            {
                                case "Bookmarks":
                                    foreach (var bookmark in value.Split(','))
                                        if (value.Length > 0)
                                        {
                                            var time = int.Parse(value[bookmark], CultureInfo.InvariantCulture);
                                            if (!beatmap.bookmarks.Contains(time))
                                                beatmap.bookmarks.Add(int.Parse(value[bookmark],
                                                    CultureInfo.InvariantCulture));
                                        }

                                    break;
                            }
                        }); break;

                    case "Metadata":
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            switch (key)
                            {
                                case "Version": beatmap.name = value.ToString(); break;

                                case "BeatmapID": beatmap.id = long.Parse(value, CultureInfo.InvariantCulture); break;
                            }
                        }); break;

                    case "Difficulty":
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            switch (key)
                            {
                                case "HPDrainRate":
                                    beatmap.hpDrainRate = float.Parse(value, CultureInfo.InvariantCulture); break;

                                case "CircleSize":
                                    beatmap.circleSize = float.Parse(value, CultureInfo.InvariantCulture); break;

                                case "OverallDifficulty":
                                    beatmap.overallDifficulty = float.Parse(value, CultureInfo.InvariantCulture); break;

                                case "ApproachRate":
                                    beatmap.approachRate = float.Parse(value, CultureInfo.InvariantCulture); break;

                                case "SliderMultiplier":
                                    beatmap.sliderMultiplier = float.Parse(value, CultureInfo.InvariantCulture); break;

                                case "SliderTickRate":
                                    beatmap.sliderTickRate = float.Parse(value, CultureInfo.InvariantCulture); break;
                            }
                        }); break;

                    case "Events":
                        reader.ParseSectionLines(line =>
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

                                        beatmap.backgroundPath = removePathQuotes(line[values.Current]);
                                        break;
                                    }

                                    case "2": beatmap.breaks.Add(OsuBreak.Parse(line)); break;
                                }
                            },
                            false); break;

                    case "TimingPoints":
                    {
                        reader.ParseSectionLines(line => beatmap.controlPoints.Add(ControlPoint.Parse(line)));

                        beatmap.controlPoints.Sort();
                        foreach (var cp in beatmap.controlPoints)
                            if (!cp.IsInherited)
                                beatmap.timingPoints.Add(cp);

                        break;
                    }

                    case "Colours":
                    {
                        beatmap.comboColors.Clear();
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            if (!key.StartsWith("Combo", StringComparison.Ordinal)) return;

                            var rgb = value.Split(',');
                            rgb.MoveNext();

                            var r = rgb.Current;
                            rgb.MoveNext();

                            var g = rgb.Current;
                            rgb.MoveNext();

                            var b = rgb.Current;

                            beatmap.comboColors.Add(Color.FromPixel(new Rgba32(
                                byte.Parse(value[r], CultureInfo.InvariantCulture),
                                byte.Parse(value[g], CultureInfo.InvariantCulture),
                                byte.Parse(value[b], CultureInfo.InvariantCulture))));
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

                                if (hitobject.NewCombo ||
                                    previousHitObject is null ||
                                    (previousHitObject.Flags & HitObjectFlag.Spinner) > 0)
                                {
                                    hitobject.Flags |= HitObjectFlag.NewCombo;

                                    var colorIncrement = hitobject.ComboOffset;
                                    if ((hitobject.Flags & HitObjectFlag.Spinner) == 0) ++colorIncrement;

                                    colorIndex = (colorIndex + colorIncrement) % beatmap.comboColors.Count;

                                    comboIndex = 1;
                                }
                                else ++comboIndex;

                                hitobject.ComboIndex = comboIndex;
                                hitobject.ColorIndex = colorIndex;
                                hitobject.Color = beatmap.comboColors[colorIndex];

                                beatmap.hitObjects.Add(hitobject);
                                previousHitObject = hitobject;
                            },
                            false);

                        break;
                    }
                }
            });

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

        var stackLenienceSquared = 9;
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
                            (spanN.PlayfieldEndPosition - objectI.PlayfieldPosition).LengthSquared < stackLenienceSquared)
                        {
                            var offset = objectI.StackIndex - objectN.StackIndex + 1;

                            for (var j = n + 1; j <= i; ++j)
                                if ((spanN.PlayfieldEndPosition - hitObjects[j].PlayfieldPosition).LengthSquared <
                                    stackLenienceSquared)
                                    hitObjects[j].StackIndex -= offset;

                            break;
                        }

                        if (!((objectN.PlayfieldPosition - objectI.PlayfieldPosition).LengthSquared <
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
                                objectI.PlayfieldPosition).LengthSquared <
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

    static string removePathQuotes(ReadOnlySpan<char> path)
        => path.StartsWith('"') && path.EndsWith('"') ? path[1..^1].ToString() : path.ToString();

    #endregion
}