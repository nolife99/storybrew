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
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

internal class ImportOsb : StoryboardObjectGenerator
{
    readonly PooledDictionary<ValueArray<char>, ValueArray<char>> vars = [];

    [Description("Path to the .osb to import, relative to the project folder."), Configurable]
    public string Path = "storyboard.osb";

    protected override void Generate()
    {
        using (var stream = OpenProjectFile(Path))
        using (StreamReader reader = new(stream, Encoding.ASCII))
            reader.ParseSections(section =>
            {
                switch (section)
                {
                    case "Variables": parseVariables(reader); break;
                    case "Events": parseEvents(reader); break;
                }
            });

        foreach (var variable in vars)
        {
            variable.Key.Dispose();
            variable.Value.Dispose();
        }

        vars.Dispose();
    }

    void parseVariables(StreamReader reader) => reader.ParseSectionLines(line =>
    {
        using var v = line.Split(['=']);
        if (v.Count == 2)
            vars[ValueArray<char>.Create(v[0].AsReadOnlySpan())] = ValueArray<char>.Create(v[1].AsReadOnlySpan());
    });

    void parseEvents(StreamReader reader)
    {
        OsbSprite sprite = null;
        var loopable = false;

        reader.ParseSectionLines(line =>
            {
                if (line.StartsWith("//")) return;

                var depth = 0;
                while (line[depth..].StartsWith(' ')) ++depth;

                using var trim = applyVariables(line.Trim());
                using var v = trim.AsReadOnlySpan().Split([',']);

                if (loopable && depth < 2)
                {
                    sprite.EndGroup();
                    loopable = false;
                }

                switch (v[0].AsReadOnlySpan())
                {
                    case "Sprite":
                    {
                        var origin = Enum.Parse<OsbOrigin>(v[2].AsReadOnlySpan());
                        var path = removeQuotes(v[3].AsReadOnlySpan());
                        var x = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var y = float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        sprite = GetLayer(v[1].AsReadOnlySpan().ToString()).CreateSprite(path.ToString(), origin, new(x, y));
                        break;
                    }

                    case "Animation":
                    {
                        var origin = Enum.Parse<OsbOrigin>(v[2].AsReadOnlySpan());
                        var path = removeQuotes(v[3].AsReadOnlySpan());
                        var x = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var y = float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var frameCount = int.Parse(v[6].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var frameDelay = float.Parse(v[7].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var loopType = Enum.Parse<OsbLoopType>(v[8].AsReadOnlySpan());
                        sprite = GetLayer(v[1].AsReadOnlySpan().ToString())
                            .CreateAnimation(path.ToString(), frameCount, frameDelay, loopType, origin, new Vector2(x, y));

                        break;
                    }

                    case "Sample":
                        GetLayer(v[2].AsReadOnlySpan().ToString())
                            .CreateSample(removeQuotes(v[3].AsReadOnlySpan()).ToString(),
                                int.Parse(v[1].AsReadOnlySpan(), CultureInfo.InvariantCulture),
                                float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture)); break;

                    case "T":
                        sprite.StartTriggerGroup(v[1].AsReadOnlySpan().ToString(),
                            int.Parse(v[2].AsReadOnlySpan(), CultureInfo.InvariantCulture),
                            int.Parse(v[3].AsReadOnlySpan(), CultureInfo.InvariantCulture),
                            v.Count > 4 ? int.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture) : 0);

                        loopable = true;
                        break;

                    case "L":
                        sprite.StartLoopGroup(int.Parse(v[1].AsReadOnlySpan(), CultureInfo.InvariantCulture),
                            int.Parse(v[2].AsReadOnlySpan(), CultureInfo.InvariantCulture));

                        loopable = true;
                        break;

                    default:
                    {
                        var command = v[0];
                        var easing = (OsbEasing)int.Parse(v[1].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var startTime = int.Parse(v[2].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                        var endTime = v[3].AsReadOnlySpan().IsEmpty ?
                            startTime :
                            int.Parse(v[3].AsReadOnlySpan(), CultureInfo.InvariantCulture);

                        switch (command.AsReadOnlySpan())
                        {
                            case "F":
                            {
                                var startValue = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.Fade(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "S":
                            {
                                var startValue = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.Scale(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "V":
                            {
                                var startX = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var startY = float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endX = v.Count > 6 ?
                                    float.Parse(v[6].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startX;

                                var endY = v.Count > 7 ?
                                    float.Parse(v[7].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startY;

                                sprite.ScaleVec(easing, startTime, endTime, startX, startY, endX, endY);
                                break;
                            }

                            case "R":
                            {
                                var startValue = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.Rotate(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "M":
                            {
                                var startX = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var startY = float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endX = v.Count > 6 ?
                                    float.Parse(v[6].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startX;

                                var endY = v.Count > 7 ?
                                    float.Parse(v[7].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startY;

                                sprite.Move(easing, startTime, endTime, startX, startY, endX, endY);
                                break;
                            }

                            case "MX":
                            {
                                var startValue = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.MoveX(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "MY":
                            {
                                var startValue = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture);
                                var endValue = v.Count > 5 ?
                                    float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                                    startValue;

                                sprite.MoveY(easing, startTime, endTime, startValue, endValue);
                                break;
                            }

                            case "C":
                            {
                                var startX = float.Parse(v[4].AsReadOnlySpan(), CultureInfo.InvariantCulture) / 255;
                                var startY = float.Parse(v[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) / 255;
                                var startZ = float.Parse(v[6].AsReadOnlySpan(), CultureInfo.InvariantCulture) / 255;
                                var endX = v.Count > 7 ?
                                    float.Parse(v[7].AsReadOnlySpan(), CultureInfo.InvariantCulture) / 255 :
                                    startX;

                                var endY = v.Count > 8 ?
                                    float.Parse(v[8].AsReadOnlySpan(), CultureInfo.InvariantCulture) / 255 :
                                    startY;

                                var endZ = v.Count > 9 ?
                                    float.Parse(v[9].AsReadOnlySpan(), CultureInfo.InvariantCulture) / 255 :
                                    startZ;

                                sprite.Color(easing, startTime, endTime, startX, startY, startZ, endX, endY, endZ);
                                break;
                            }

                            case "P":
                            {
                                switch (v[4].AsReadOnlySpan())
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

                foreach (var value in v) value.Dispose();
            },
            false);

        if (!loopable) return;

        sprite.EndGroup();
        loopable = false;
    }

    static ReadOnlySpan<char> removeQuotes(ReadOnlySpan<char> path)
        => path.StartsWith('"') && path.EndsWith('"') ? path[1..^1] : path;

    TempList<char> applyVariables(ReadOnlySpan<char> line)
    {
        if (!line.Contains('$')) return TempList<char>.Create(line);

        var result = TempList<char>.Create();

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