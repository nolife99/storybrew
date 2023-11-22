using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Util;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;

namespace StorybrewScripts
{
    class ImportOsb : StoryboardObjectGenerator
    {
        [Description("Path to the .osb to import, relative to the project folder.")]
        [Configurable] public string Path = "storyboard.osb";
        readonly Dictionary<string, string> vars = [];

        protected override void Generate()
        {
            using var stream = OpenProjectFile(Path); 
            using var reader = new StreamReader(stream, Encoding.ASCII); 
            
            reader.ParseSections(section =>
            {
                switch (section)
                {
                    case "Variables": parseVariables(reader); break;
                    case "Events": parseEvents(reader); break;
                }
            });
            vars.Clear();
        }
        void parseVariables(StreamReader reader) => reader.ParseSectionLines(line =>
        {
            var v = line.Split('=');
            if (v.Length == 2) vars[v[0]] = v[1];
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

                var trim = applyVariables(line.Trim());
                var v = trim.Split(',');

                if (loopable && depth < 2)
                {
                    sprite.EndGroup();
                    loopable = false;
                }

                switch (v[0])
                {
                    case "Sprite":
                    {
                        var origin = (OsbOrigin)Enum.Parse(typeof(OsbOrigin), v[2]);
                        var path = removeQuotes(v[3]);
                        var x = float.Parse(v[4], CultureInfo.InvariantCulture);
                        var y = float.Parse(v[5], CultureInfo.InvariantCulture);
                        sprite = GetLayer(v[1]).CreateSprite(path, origin, new Vector2(x, y));
                        break;
                    }
                    case "Animation":
                    {
                        var origin = (OsbOrigin)Enum.Parse(typeof(OsbOrigin), v[2]);
                        var path = removeQuotes(v[3]);
                        var x = float.Parse(v[4], CultureInfo.InvariantCulture);
                        var y = float.Parse(v[5], CultureInfo.InvariantCulture);
                        var frameCount = int.Parse(v[6], CultureInfo.InvariantCulture);
                        var frameDelay = float.Parse(v[7], CultureInfo.InvariantCulture);
                        var loopType = (OsbLoopType)Enum.Parse(typeof(OsbLoopType), v[8]);
                        sprite = GetLayer(v[1]).CreateAnimation(path, frameCount, frameDelay, loopType, origin, new Vector2(x, y));
                        break;
                    }
                    case "Sample":
                        GetLayer(v[2]).CreateSample(removeQuotes(v[3]), int.Parse(v[1], CultureInfo.InvariantCulture), float.Parse(v[4], CultureInfo.InvariantCulture));
                        break;

                    case "T":
                        sprite.StartTriggerGroup(v[1], int.Parse(v[2], CultureInfo.InvariantCulture), int.Parse(v[3], CultureInfo.InvariantCulture), v.Length > 4 ? int.Parse(v[4], CultureInfo.InvariantCulture) : 0);
                        loopable = true;
                        break;

                    case "L":
                        sprite.StartLoopGroup(int.Parse(v[1], CultureInfo.InvariantCulture), int.Parse(v[2], CultureInfo.InvariantCulture));
                        loopable = true;
                        break;

                    default:
                    {
                        if (string.IsNullOrEmpty(v[3])) v[3] = v[2];

                        var command = v[0];
                        var easing = (OsbEasing)int.Parse(v[1], CultureInfo.InvariantCulture);
                        var startTime = int.Parse(v[2], CultureInfo.InvariantCulture);
                        var endTime = int.Parse(v[3], CultureInfo.InvariantCulture);

                        switch (command)
                        {
                            case "F":
                            {
                                var startValue = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var endValue = v.Length > 5 ? float.Parse(v[5], CultureInfo.InvariantCulture) : startValue;
                                sprite.Fade(easing, startTime, endTime, startValue, endValue);
                                break;
                            }
                            case "S":
                            {
                                var startValue = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var endValue = v.Length > 5 ? float.Parse(v[5], CultureInfo.InvariantCulture) : startValue;
                                sprite.Scale(easing, startTime, endTime, startValue, endValue);
                                break;
                            }
                            case "V":
                            {
                                var startX = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var startY = float.Parse(v[5], CultureInfo.InvariantCulture);
                                var endX = v.Length > 6 ? float.Parse(v[6], CultureInfo.InvariantCulture) : startX;
                                var endY = v.Length > 7 ? float.Parse(v[7], CultureInfo.InvariantCulture) : startY;
                                sprite.ScaleVec(easing, startTime, endTime, startX, startY, endX, endY);
                                break;
                            }
                            case "R":
                            {
                                var startValue = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var endValue = v.Length > 5 ? float.Parse(v[5], CultureInfo.InvariantCulture) : startValue;
                                sprite.Rotate(easing, startTime, endTime, startValue, endValue);
                                break;
                            }
                            case "M":
                            {
                                var startX = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var startY = float.Parse(v[5], CultureInfo.InvariantCulture);
                                var endX = v.Length > 6 ? float.Parse(v[6], CultureInfo.InvariantCulture) : startX;
                                var endY = v.Length > 7 ? float.Parse(v[7], CultureInfo.InvariantCulture) : startY;
                                sprite.Move(easing, startTime, endTime, startX, startY, endX, endY);
                                break;
                            }
                            case "MX":
                            {
                                var startValue = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var endValue = v.Length > 5 ? float.Parse(v[5], CultureInfo.InvariantCulture) : startValue;
                                sprite.MoveX(easing, startTime, endTime, startValue, endValue);
                                break;
                            }
                            case "MY":
                            {
                                var startValue = float.Parse(v[4], CultureInfo.InvariantCulture);
                                var endValue = v.Length > 5 ? float.Parse(v[5], CultureInfo.InvariantCulture) : startValue;
                                sprite.MoveY(easing, startTime, endTime, startValue, endValue);
                                break;
                            }
                            case "C":
                            {
                                var startX = float.Parse(v[4], CultureInfo.InvariantCulture) / 255;
                                var startY = float.Parse(v[5], CultureInfo.InvariantCulture) / 255;
                                var startZ = float.Parse(v[6], CultureInfo.InvariantCulture) / 255;
                                var endX = v.Length > 7 ? float.Parse(v[7], CultureInfo.InvariantCulture) / 255 : startX;
                                var endY = v.Length > 8 ? float.Parse(v[8], CultureInfo.InvariantCulture) / 255 : startY;
                                var endZ = v.Length > 9 ? float.Parse(v[9], CultureInfo.InvariantCulture) / 255 : startZ;
                                sprite.Color(easing, startTime, endTime, startX, startY, startZ, endX, endY, endZ);
                                break;
                            }
                            case "P":
                            {
                                switch (v[4])
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
            }, false);

            if (loopable)
            {
                sprite.EndGroup();
                loopable = false;
            }
        }

        static string removeQuotes(string path) => path.StartsWith('"') && path.EndsWith('"') ? path[1..^1] : path;
        string applyVariables(string line)
        {
            if (!line.Contains('$')) return line;
            foreach (var entry in vars) line = line.Replace(entry.Key, entry.Value);
            return line;
        }
    }
}