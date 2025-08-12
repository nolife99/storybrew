namespace StorybrewScripts;

using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

class ImportOsb : StoryboardObjectGenerator
{
    readonly PooledDictionary<ValueArray<char>, ValueArray<char>> vars = [];

    [Description("Path to the .osb to import, relative to the project folder."), Configurable]
    public string Path = "storyboard.osb";

    protected override void Generate()
    {
        using (var stream = OpenProjectFile(Path))
        using (StreamReader reader = new(stream, Encoding.ASCII))
            reader.ParseSections((section, state) =>
                {
                    switch (section)
                    {
                        case "Variables": state.Item2.parseVariables(state.reader); break;
                        case "Events": state.Item2.parseEvents(state.reader); break;
                    }
                },
                (reader, this));

        foreach (var variable in vars)
        {
            variable.Key.Dispose();
            variable.Value.Dispose();
        }

        vars.Dispose();
    }

    void parseVariables(StreamReader reader) => reader.ParseSectionLines((line, state) =>
        {
            using var v = line.Split(['=']);
            if (v.Count == 2) state.vars[ValueArray.Create(line[v[0]])] = ValueArray.Create(line[v[1]]);
        },
        this);

    void parseEvents(StreamReader reader)
    {
        OsbSprite sprite = null;
        var loopable = false;

        reader.ParseSectionLines((line, state) =>
            {
                if (line.StartsWith("//")) return;

                var depth = 0;
                while (line[depth..].StartsWith(' ')) ++depth;

                using var trimStr = state.applyVariables(line.Trim());

                var trim = trimStr.AsReadOnlySpan();
                using var v = trim.Split([',']);

                if (loopable && depth < 2)
                {
                    sprite.EndGroup();
                    loopable = false;
                }

                switch (trim[v[0]])
                {
                    case "Sprite":
                    {
                        var origin = Enum.Parse<OsbOrigin>(trim[v[2]]);
                        var path = removeQuotes(trim[v[3]]);
                        var x = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                        var y = float.Parse(trim[v[5]], CultureInfo.InvariantCulture);
                        sprite = state.GetLayer(trim[v[1]].ToString()).CreateSprite(path.ToString(), origin, new(x, y));

                        break;
                    }

                    case "Animation":
                    {
                        var origin = Enum.Parse<OsbOrigin>(trim[v[2]]);
                        var path = removeQuotes(trim[v[3]]);
                        var x = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                        var y = float.Parse(trim[v[5]], CultureInfo.InvariantCulture);
                        var frameCount = int.Parse(trim[v[6]], CultureInfo.InvariantCulture);
                        var frameDelay = float.Parse(trim[v[7]], CultureInfo.InvariantCulture);
                        var loopType = Enum.Parse<OsbLoopType>(trim[v[8]]);
                        sprite = state.GetLayer(trim[v[1]].ToString())
                            .CreateAnimation(path.ToString(), frameCount, frameDelay, loopType, origin, new Vector2(x, y));

                        break;
                    }

                    case "Sample":
                        state.GetLayer(trim[v[2]].ToString())
                            .CreateSample(removeQuotes(trim[v[3]]).ToString(),
                                int.Parse(trim[v[1]], CultureInfo.InvariantCulture),
                                float.Parse(trim[v[4]], CultureInfo.InvariantCulture)); break;

                    case "T":
                        sprite.StartTriggerGroup(trim[v[1]].ToString(),
                            int.Parse(trim[v[2]], CultureInfo.InvariantCulture),
                            int.Parse(trim[v[3]], CultureInfo.InvariantCulture),
                            v.Count > 4 ? int.Parse(trim[v[4]], CultureInfo.InvariantCulture) : 0);

                        loopable = true;
                        break;

                    case "L":
                        sprite.StartLoopGroup(int.Parse(trim[v[1]], CultureInfo.InvariantCulture),
                            int.Parse(trim[v[2]], CultureInfo.InvariantCulture));

                        loopable = true;
                        break;

                    default:
                    {
                        var command = v[0];
                        var easing = (OsbEasing)int.Parse(trim[v[1]], CultureInfo.InvariantCulture);
                        var startTime = int.Parse(trim[v[2]], CultureInfo.InvariantCulture);
                        var endTime = trim[v[3]].IsEmpty ? startTime : int.Parse(trim[v[3]], CultureInfo.InvariantCulture);

                        switch (trim[command])
                        {
                            case "F":
                            {
                                var startValue = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(trim[v[5]], CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.Fade(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "S":
                            {
                                var startValue = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(trim[v[5]], CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.Scale(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "V":
                            {
                                var startX = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var startY = float.Parse(trim[v[5]], CultureInfo.InvariantCulture);
                                var endX = v.Count > 6 ? float.Parse(trim[v[6]], CultureInfo.InvariantCulture) : startX;

                                var endY = v.Count > 7 ? float.Parse(trim[v[7]], CultureInfo.InvariantCulture) : startY;

                                sprite.ScaleVec(easing, startTime, endTime, startX, startY, endX, endY);
                                break;
                            }

                            case "R":
                            {
                                var startValue = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(trim[v[5]], CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.Rotate(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "M":
                            {
                                var startX = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var startY = float.Parse(trim[v[5]], CultureInfo.InvariantCulture);
                                var endX = v.Count > 6 ? float.Parse(trim[v[6]], CultureInfo.InvariantCulture) : startX;

                                var endY = v.Count > 7 ? float.Parse(trim[v[7]], CultureInfo.InvariantCulture) : startY;

                                sprite.Move(easing, startTime, endTime, startX, startY, endX, endY);
                                break;
                            }

                            case "MX":
                            {
                                var startValue = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(trim[v[5]], CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.MoveX(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "MY":
                            {
                                var startValue = float.Parse(trim[v[4]], CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(trim[v[5]], CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.MoveY(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "C":
                            {
                                var startX = float.Parse(trim[v[4]], CultureInfo.InvariantCulture) / 255;
                                var startY = float.Parse(trim[v[5]], CultureInfo.InvariantCulture) / 255;
                                var startZ = float.Parse(trim[v[6]], CultureInfo.InvariantCulture) / 255;
                                var endX = v.Count > 7 ?
                                    float.Parse(trim[v[7]], CultureInfo.InvariantCulture) / 255 :
                                    startX;

                                var endY = v.Count > 8 ?
                                    float.Parse(trim[v[8]], CultureInfo.InvariantCulture) / 255 :
                                    startY;

                                var endZ = v.Count > 9 ?
                                    float.Parse(trim[v[9]], CultureInfo.InvariantCulture) / 255 :
                                    startZ;

                                sprite.Color(easing, startTime, endTime, startX, startY, startZ, endX, endY, endZ);
                                break;
                            }

                            case "P":
                            {
                                switch (trim[v[4]])
                                {
                                    case "A": sprite.Additive(startTime, endTime); break;
                                    case "H": sprite.FlipH(startTime, endTime); break;
                                    case "V": sprite.FlipV(startTime, endTime); break;
                                }

                                break;
                            }
                        }
                    }

                        break;
                }
            },
            this,
            false);

        if (!loopable) return;

        sprite.EndGroup();
        loopable = false;
    }

    static ReadOnlySpan<char> removeQuotes(ReadOnlySpan<char> path)
        => path.StartsWith('"') && path.EndsWith('"') ? path[1..^1] : path;

    TempList<char> applyVariables(ReadOnlySpan<char> line)
    {
        if (!line.Contains('$')) return TempList.Create(line);

        var result = TempList.Create<char>();

        var currentPos = 0;

        while (currentPos < line.Length)
        {
            var replaced = false;
            foreach (var entry in vars)
            {
                var key = entry.Key.AsReadOnlySpan();
                if (currentPos + key.Length > line.Length ||
                    !line.Slice(currentPos, key.Length).SequenceEqual(key)) continue;

                result.AddRange(entry.Value.AsReadOnlySpan());
                currentPos += key.Length;
                replaced = true;
                break;
            }

            if (replaced) continue;

            result.Add(line[currentPos++]);
        }

        return result;
    }
}