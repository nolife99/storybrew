using BrewLib.Util;
using osuTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using System.IO;
using System.Linq;
using Tiny;
using System.Globalization;

namespace StorybrewCommon.Subtitles
{
    ///<summary> Stores information about a font image. </summary>
    ///<remarks> Creates a new <see cref="FontTexture"/> storing information of the texture. </remarks>
    ///<param name="path"> The path to the font texture. </param>
    ///<param name="offsetX"> The texture offset in X-units. </param>
    ///<param name="offsetY"> The texture offset in Y-units. </param>
    ///<param name="baseWidth"> The base width of the texture in pixels. </param>
    ///<param name="baseHeight"> The base height of the texture in pixels. </param>
    ///<param name="width"> The actual width of the texture in pixels. </param>
    ///<param name="height"> The actual width of the texture in pixels. </param>
    public class FontTexture(string path, float offsetX, float offsetY, int baseWidth, int baseHeight, int width, int height)
    {
        ///<summary> The path to the font texture. </summary>
        public readonly string Path = path;

        ///<returns> <see langword="true"/> if the texture does not exist. </returns>
        public bool IsEmpty => Path is null;

        ///<summary> The texture offset in X-units. </summary>
        public readonly float OffsetX = offsetX;

        ///<summary> The texture offset in Y-units. </summary>
        public readonly float OffsetY = offsetY;

        ///<summary> The original width of the texture in pixels. </summary>
        public readonly int BaseWidth = baseWidth;

        ///<summary> The original height of the texture in pixels. </summary>
        public readonly int BaseHeight = baseHeight;

        ///<summary> The actual width of the texture in pixels. </summary>
        public readonly int Width = width;

        ///<summary> The actual width of the texture in pixels. </summary>
        public readonly int Height = height;

        ///<summary> Gets the font offset for the given <see cref="OsbOrigin"/>. </summary>
        public Vector2 OffsetFor(OsbOrigin origin) => origin switch
        {
            OsbOrigin.TopCentre => new(OffsetX + Width * .5f, OffsetY),
            OsbOrigin.TopRight => new(OffsetX + Width, OffsetY),
            OsbOrigin.CentreLeft => new(OffsetX, OffsetY + Height * .5f),
            OsbOrigin.Centre => new(OffsetX + Width * .5f, OffsetY + Height * .5f),
            OsbOrigin.CentreRight => new(OffsetX + Width, OffsetY + Height * .5f),
            OsbOrigin.BottomLeft => new(OffsetX, OffsetY + Height),
            OsbOrigin.BottomCentre => new(OffsetX + Width * .5f, OffsetY + Height),
            OsbOrigin.BottomRight => new(OffsetX + Width, OffsetY + Height),
            _ => new(OffsetX, OffsetY),
        };
    }

    ///<summary> Base struct for coloring commands. </summary>
    [Serializable] public readonly struct FontColor : IEquatable<FontColor>
    {
        ///<summary> Represents a <see cref="FontColor"/> value as the color black. </summary>
        public static readonly FontColor Black = new(0, 0, 0);

        ///<summary> Represents a <see cref="FontColor"/> value as the color white. </summary>
        public static readonly FontColor White = new(1, 1, 1);

        ///<summary> Represents a <see cref="FontColor"/> value as the color red. </summary>
        public static readonly FontColor Red = new(1, 0, 0);

        ///<summary> Represents a <see cref="FontColor"/> value as the color green. </summary>
        public static readonly FontColor Green = new(0, 1, 0);

        ///<summary> Represents a <see cref="FontColor"/> value as the color blue. </summary>
        public static readonly FontColor Blue = new(0, 0, 1);

        readonly float r, g, b, a;

        ///<summary> Gets the red value of this instance. </summary>
        public byte R => toByte(r);

        ///<summary> Gets the green value of this instance. </summary>
        public byte G => toByte(g);

        ///<summary> Gets the blue value of this instance. </summary>
        public byte B => toByte(b);

        ///<summary> Gets the blue value of this instance. </summary>
        public byte A => toByte(a);

        ///<summary> Constructs a new <see cref="CommandColor"/> from red, green, and blue values from 0.0 to 1.0. </summary>
        public FontColor(float r = 1, float g = 1, float b = 1, float a = 1)
        {
            if (float.IsNaN(r) || float.IsInfinity(r) ||
                float.IsNaN(g) || float.IsInfinity(g) ||
                float.IsNaN(b) || float.IsInfinity(b) ||
                float.IsNaN(a) || float.IsInfinity(a) ||
                r > 1 || g > 1 || b > 1 || a > 1)
                throw new ArgumentException($"Invalid font color {r}, {g}, {b}");

            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        ///<summary> Returns whether or not this instance and <paramref name="other"/> are equal to each other. </summary>
        public bool Equals(FontColor other) => r == other.r && g == other.g && b == other.b;

        ///<summary> Returns whether or not this instance and <paramref name="obj"/> are equal to each other. </summary>
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            return obj is CommandColor color && Equals(color);
        }

        ///<summary> Returns a 32-bit integer hash that represents this instance's color information, with 8 bits per channel. </summary>
        public override int GetHashCode() => ((Color)this).ToArgb();

        ///<summary> Converts this instance into a string, formatted as "R, G, B". </summary>
        public override string ToString() => $"{R}, {G}, {B}";

        ///<summary> Returns a <see cref="FontColor"/> structure that represents the hash code's color information. </summary>
        public static FontColor FromHashCode(int code) => Color.FromArgb(code);

        ///<summary> Creates a <see cref="FontColor"/> from RGB byte values. </summary>
        public static FontColor FromRgba(byte r, byte g, byte b, byte a) => new(r / 255f, g / 255f, b / 255f, a / 255f);

        ///<summary> Creates a <see cref="Vector4"/> containing the hue, saturation, brightness, and alpha values from a <see cref="FontColor"/>. </summary>
        public static Vector4 ToHsb(FontColor rgb)
        {
            var max = Math.Max(rgb.r, Math.Max(rgb.g, rgb.b));
            var min = Math.Min(rgb.r, Math.Min(rgb.g, rgb.b));
            var delta = max - min;

            var hue = 0f;
            if (rgb.r == max) hue = (rgb.g - rgb.b) / delta;
            else if (rgb.g == max) hue = 2 + (rgb.b - rgb.r) / delta;
            else if (rgb.b == max) hue = 4 + (rgb.r - rgb.g) / delta;
            hue /= 6;
            if (hue < 0f) ++hue;

            var saturation = float.IsNegative(max) ? 0 : 1 - (min / max);

            return new Vector4(hue, saturation, max, rgb.a);
        }

        ///<summary> Creates a <see cref="FontColor"/> from a hex-code color. </summary>
        public static FontColor FromHtml(string htmlColor) => ColorTranslator.FromHtml(htmlColor.StartsWith('#') ? htmlColor : "#" + htmlColor);

        static byte toByte(float x) => (byte)osuTK.MathHelper.Clamp(x * 255, 0, 255);

#pragma warning disable CS1591
        public static implicit operator Color4(FontColor obj) => new(obj.R, obj.G, obj.B, obj.A);
        public static implicit operator FontColor(Color4 obj) => new(obj.R, obj.G, obj.B, obj.A);
        public static implicit operator FontColor(Color obj) => new(obj.R / 255f, obj.G / 255f, obj.B / 255f, obj.A / 255f);
        public static implicit operator Color(FontColor obj) => Color.FromArgb(obj.A, obj.R, obj.G, obj.B);
        public static implicit operator FontColor(string hexCode) => FromHtml(hexCode);
        public static implicit operator FontColor(int channel) => FromHashCode(channel);
        public static bool operator ==(FontColor left, FontColor right) => left.Equals(right);
        public static bool operator !=(FontColor left, FontColor right) => !left.Equals(right);
        public static FontColor operator +(FontColor left, FontColor right) => new(left.r + right.r, left.g + right.g, left.b + right.b, left.a + right.a);
        public static FontColor operator -(FontColor left, FontColor right) => new(left.r - right.r, left.g - right.g, left.b - right.b, left.a - right.a);
        public static FontColor operator *(FontColor left, FontColor right) => new(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
        public static FontColor operator *(FontColor left, double right) => new((float)(left.r * right), (float)(left.g * right), (float)(left.b * right), (float)(left.a * right));
        public static FontColor operator *(double left, FontColor right) => right * left;
        public static FontColor operator /(FontColor left, double right) => new((float)(left.r / right), (float)(left.g / right), (float)(left.b / right), (float)(left.a / right));
    }

    /// <summary> Stores information about a font's appearance. </summary>
    ///<remarks> Creates a new <see cref="FontDescription"/> storing a descriptor for <see cref="FontGenerator"/>. </remarks>
    ///<param name="fontPath"> The path to the font file. </param>
    ///<param name="fontSize"> The relative size of the font. </param>
    ///<param name="color"> The coloring tint of the font. </param>
    ///<param name="padding"> Allocate extra space around the font when generating it. </param>
    ///<param name="fontStyle"> Format/style of the font. </param>
    ///<param name="trimTransparency"> Trim transparent space around the font. </param>
    ///<param name="effectsOnly"> Leave out the original font and keep the effects. </param>
    ///<param name="debug"> Draw a randomly colored background behind the font. </param>
    public class FontDescription(
        string fontPath, int fontSize = 76, FontColor color = default, Vector2 padding = default,
        FontStyle fontStyle = default, bool trimTransparency = true, bool effectsOnly = false, bool debug = false)
    {
        ///<summary> The path to the font file. </summary>
        public readonly string FontPath = fontPath;

        ///<summary> The relative size of the font. </summary>
        public readonly int FontSize = fontSize;

        ///<summary> The coloring tint of the font. </summary>
        public readonly FontColor Color = color;

        ///<summary> How much extra space is allocated around the font when generating it. </summary>
        public readonly Vector2 Padding = padding;

        ///<summary> The format/style of the font (for example: bold, italics, etc). </summary>
        public readonly FontStyle FontStyle = fontStyle;

        ///<summary> Trim transparent space around the font. Should always be <see langword="true"/>. </summary>
        public readonly bool TrimTransparency = trimTransparency;

        ///<summary> Leave out the original font and keep the effects. </summary>
        public readonly bool EffectsOnly = effectsOnly;

        ///<summary> Draw a randomly colored background behind the font. </summary>
        public readonly bool Debug = debug;
    }

    ///<summary> A class that generates and manages font textures. </summary>
    public class FontGenerator
    {
        /// <summary> The directory to the font textures. </summary>
        public readonly string Directory;

        readonly FontDescription description;
        readonly FontEffect[] effects;
        readonly string projectDirectory, assetDirectory;
        internal readonly Dictionary<string, FontTexture> cache;

        internal FontGenerator(string directory, FontDescription description, FontEffect[] effects, string projectDirectory, string assetDirectory)
        {
            Directory = directory;
            this.description = description;
            this.effects = effects;
            this.projectDirectory = projectDirectory;
            this.assetDirectory = assetDirectory;
            cache = [];
        }

        ///<summary> Gets the texture path of the matching item's string representation. </summary>
        public FontTexture GetTexture(object obj)
        {
            var text = Convert.ToString(obj, CultureInfo.InvariantCulture);
            if (!cache.TryGetValue(text, out var texture)) cache[text] = texture = generateTexture(text);
            return texture;
        }
        FontTexture generateTexture(string text)
        {
            // wtf is this
            var filename = text.Length == 1 ?
                $"{(!PathHelper.IsValidFilename(text[0].ToString(CultureInfo.InvariantCulture)) ? ((int)text[0]).ToString("x4", CultureInfo.InvariantCulture).TrimStart('0') : (char.IsUpper(text[0]) ? char.ToLower(text[0], CultureInfo.InvariantCulture) + '_' : text[0].ToString(CultureInfo.InvariantCulture)))}.png" :
                $"_{cache.Count(l => l.Key.Length > 1).ToString("x3", CultureInfo.InvariantCulture).TrimStart('0')}.png";

            var trimExist = false;
            if (description.TrimTransparency && cache.TryGetValue(text.Trim(), out var texture))
            {
                trimExist = true;
                filename = Path.GetFileName(texture.Path);
            }

            var path = Path.Combine(assetDirectory, Directory, filename);

            var dir = Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            var fontPath = Path.Combine(projectDirectory, description.FontPath);
            if (!File.Exists(fontPath)) fontPath = description.FontPath;

            float offsetX = 0, offsetY = 0;
            int baseWidth, baseHeight, width, height;

            using (StringFormat format = new(StringFormat.GenericTypographic)) using (PrivateFontCollection fontCollection = new()) using (var graphics = Graphics.FromHwnd(0))
            {
                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                format.Alignment = StringAlignment.Center;
                format.FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;

                FontFamily family = null;
                if (File.Exists(fontPath))
                {
                    fontCollection.AddFontFile(fontPath);
                    family = fontCollection.Families[0];
                }

                var dpiScale = 96 / graphics.DpiY;
                using Font font = family is not null ? new(family, description.FontSize * dpiScale, description.FontStyle) : new(fontPath, description.FontSize * dpiScale, description.FontStyle);

                var measuredSize = graphics.MeasureString(text, font, 0, format);
                baseWidth = (int)Math.Ceiling(measuredSize.Width);
                baseHeight = (int)Math.Ceiling(measuredSize.Height);

                float effectsWidth = 0, effectsHeight = 0;
                for (var i = 0; i < effects.Length; ++i)
                {
                    var effectSize = effects[i].Measure;
                    effectsWidth = Math.Max(effectsWidth, effectSize.Width);
                    effectsHeight = Math.Max(effectsHeight, effectSize.Height);
                }
                width = (int)Math.Ceiling(baseWidth + effectsWidth + description.Padding.X * 2);
                height = (int)Math.Ceiling(baseHeight + effectsHeight + description.Padding.Y * 2);

                var paddingX = description.Padding.X + effectsWidth / 2;
                var paddingY = description.Padding.Y + effectsHeight / 2;
                var x = paddingX + measuredSize.Width / 2;
                var y = paddingY;

                offsetX = -paddingX;
                offsetY = -paddingY;

                if (text.Length == 1 && char.IsWhiteSpace(text[0]) || width == 0 || height == 0) return new(null, offsetX, offsetY, baseWidth, baseHeight, width, height);

                using Bitmap bitmap = new(width, height);
                using (var textGraphics = Graphics.FromImage(bitmap))
                {
                    textGraphics.TextRenderingHint = graphics.TextRenderingHint;
                    textGraphics.SmoothingMode = graphics.SmoothingMode;
                    textGraphics.InterpolationMode = graphics.InterpolationMode;

                    if (description.Debug)
                    {
                        FastRandom r = new(cache.Count);
                        textGraphics.Clear(Color.FromArgb(r.Next(100, 255), r.Next(100, 255), r.Next(100, 255)));
                    }

                    for (var i = 0; i < effects.Length; ++i) if (!effects[i].Overlay) effects[i].Draw(bitmap, textGraphics, font, format, text, x, y);
                    if (!description.EffectsOnly) using (SolidBrush draw = new(description.Color)) textGraphics.DrawString(text, font, draw, x, y, format);
                    for (var i = 0; i < effects.Length; ++i) if (effects[i].Overlay) effects[i].Draw(bitmap, textGraphics, font, format, text, x, y);

                    if (description.Debug) using (Pen pen = new(Color.Red))
                    {
                        textGraphics.DrawLine(pen, x, y, x, y + baseHeight);
                        textGraphics.DrawLine(pen, x - baseWidth * .5f, y, x + baseWidth * .5f, y);
                    }
                }

                var bounds = description.TrimTransparency ? BitmapHelper.FindTransparencyBounds(bitmap) : default;
                var validBounds = !bounds.IsEmpty && !bounds.Equals(new(default, bitmap.Size));
                if (validBounds)
                {
                    offsetX += bounds.Left;
                    offsetY += bounds.Top;
                    width = bounds.Width;
                    height = bounds.Height;
                }

                if (!trimExist)
                {
                    using (var stream = File.Create(path, bitmap.Width * bitmap.Height))
                    {
                        if (validBounds) using (var trim = bitmap.FastCloneSection(bounds)) Misc.WithRetries(() => trim.Save(stream, ImageFormat.Png));
                        else Misc.WithRetries(() => bitmap.Save(stream, ImageFormat.Png));
                    }
                    StoryboardObjectGenerator.Current.Compressor.LosslessCompress(path, new(
                        path.Contains(StoryboardObjectGenerator.Current.MapsetPath) || path.Contains(StoryboardObjectGenerator.Current.AssetPath) ? 7 : 2));
                }
            }
            return new(PathHelper.WithStandardSeparators(Path.Combine(Directory, filename)), offsetX, offsetY, baseWidth, baseHeight, width, height);
        }
        internal void HandleCache(TinyToken cachedFontRoot)
        {
            if (!matches(cachedFontRoot)) return;

            foreach (var cacheEntry in cachedFontRoot.Values<TinyObject>("Cache"))
            {
                var path = cacheEntry.Value<string>("Path");
                var hash = cacheEntry.Value<string>("Hash");

                var fullPath = Path.Combine(assetDirectory, path);
                if (!File.Exists(fullPath) || HashHelper.GetFileMd5(fullPath) != hash) continue;

                var text = cacheEntry.Value<string>("Text");
                if (text.Contains('\ufffd'))
                {
                    Trace.WriteLine($"Ignoring invalid font texture \"{text}\" ({path})");
                    continue;
                }
                if (cache.ContainsKey(text)) throw new InvalidDataException($"The font texture for \"{text}\" ({path}) has been cached multiple times");

                cache[text] = new(path,
                    cacheEntry.Value<float>("OffsetX"), cacheEntry.Value<float>("OffsetY"),
                    cacheEntry.Value<int>("BaseWidth"), cacheEntry.Value<int>("BaseHeight"),
                    cacheEntry.Value<int>("Width"), cacheEntry.Value<int>("Height"));
            }
        }
        bool matches(TinyToken cachedFontRoot)
        {
            if (cachedFontRoot.Value<string>("FontPath") == description.FontPath &&
                cachedFontRoot.Value<int>("FontSize") == description.FontSize &&
                MathUtil.FloatEquals(cachedFontRoot.Value<float>("ColorR"), description.Color.R, .00001f) &&
                MathUtil.FloatEquals(cachedFontRoot.Value<float>("ColorG"), description.Color.G, .00001f) &&
                MathUtil.FloatEquals(cachedFontRoot.Value<float>("ColorB"), description.Color.B, .00001f) &&
                MathUtil.FloatEquals(cachedFontRoot.Value<float>("ColorA"), description.Color.A, .00001f) &&
                MathUtil.FloatEquals(cachedFontRoot.Value<float>("PaddingX"), description.Padding.X, .00001f) &&
                MathUtil.FloatEquals(cachedFontRoot.Value<float>("PaddingY"), description.Padding.Y, .00001f) &&
                cachedFontRoot.Value<FontStyle>("FontStyle") == description.FontStyle &&
                cachedFontRoot.Value<bool>("TrimTransparency") == description.TrimTransparency &&
                cachedFontRoot.Value<bool>("EffectsOnly") == description.EffectsOnly &&
                cachedFontRoot.Value<bool>("Debug") == description.Debug)
            {
                var effectsRoot = cachedFontRoot.Value<TinyArray>("Effects");
                if (effectsRoot.Count != effects.Length) return false;

                for (var i = 0; i < effects.Length; ++i) if (!matches(effects[i], effectsRoot[i].Value<TinyToken>())) return false;
                return true;
            }
            return false;
        }

        static bool matches(FontEffect fontEffect, TinyToken cache)
        {
            var effectType = fontEffect.GetType();
            if (cache.Value<string>("Type") != effectType.FullName) return false;

            foreach (var field in effectType.GetFields())
            {
                var fieldType = field.FieldType;
                if (fieldType == typeof(FontColor))
                {
                    var color = (FontColor)field.GetValue(fontEffect);
                    if (!MathUtil.FloatEquals(cache.Value<float>($"{field.Name}R"), color.R, .00001f) ||
                        !MathUtil.FloatEquals(cache.Value<float>($"{field.Name}G"), color.G, .00001f) ||
                        !MathUtil.FloatEquals(cache.Value<float>($"{field.Name}B"), color.B, .00001f) ||
                        !MathUtil.FloatEquals(cache.Value<float>($"{field.Name}A"), color.A, .00001f))
                        return false;
                }
                else if (fieldType == typeof(Vector3))
                {
                    var vector = (Vector3)field.GetValue(fontEffect);
                    if (!MathUtil.FloatEquals(cache.Value<float>($"{field.Name}X"), vector.X, .00001f) ||
                        !MathUtil.FloatEquals(cache.Value<float>($"{field.Name}Y"), vector.Y, .00001f) ||
                        !MathUtil.FloatEquals(cache.Value<float>($"{field.Name}Z"), vector.Z, .00001f))
                        return false;
                }
                else if (fieldType == typeof(Vector2))
                {
                    var vector = (Vector2)field.GetValue(fontEffect);
                    if (!MathUtil.FloatEquals(cache.Value<float>($"{field.Name}X"), vector.X, .00001f) ||
                        !MathUtil.FloatEquals(cache.Value<float>($"{field.Name}Y"), vector.Y, .00001f))
                        return false;
                }
                else if (fieldType == typeof(double))
                {
                    if (!MathUtil.DoubleEquals(cache.Value<double>(field.Name), (double)field.GetValue(fontEffect), .00001)) return false;
                }
                else if (fieldType == typeof(float))
                {
                    if (!MathUtil.FloatEquals(cache.Value<float>(field.Name), (float)field.GetValue(fontEffect), .00001f)) return false;
                }
                else if (fieldType == typeof(int) || fieldType.IsEnum)
                {
                    if (cache.Value<int>(field.Name) != (int)field.GetValue(fontEffect)) return false;
                }
                else if (fieldType == typeof(string))
                {
                    if (cache.Value<string>(field.Name) != (string)field.GetValue(fontEffect)) return false;
                }
                else throw new InvalidDataException($"Unexpected field type {fieldType} for {field.Name} in {effectType.FullName}");
            }
            return true;
        }
        internal TinyObject ToTinyObject() => new()
        {
            { "FontPath", PathHelper.WithStandardSeparators(description.FontPath) },
            { "FontSize", description.FontSize },
            { "ColorR", description.Color.R },
            { "ColorG", description.Color.G },
            { "ColorB", description.Color.B },
            { "ColorA", description.Color.A },
            { "PaddingX", description.Padding.X },
            { "PaddingY", description.Padding.Y },
            { "FontStyle", description.FontStyle },
            { "TrimTransparency", description.TrimTransparency },
            { "EffectsOnly", description.EffectsOnly },
            { "Debug", description.Debug },
            { "Effects", effects.Select(fontEffectToTinyObject)},
            { "Cache", cache.Where(l => !l.Value.IsEmpty).Select(letterToTinyObject)}
        };
        TinyObject letterToTinyObject(KeyValuePair<string, FontTexture> letterEntry) => new()
        {
            { "Text", letterEntry.Key },
            { "Path", PathHelper.WithStandardSeparators(letterEntry.Value.Path) },
            { "Hash", HashHelper.GetFileMd5(Path.Combine(assetDirectory, letterEntry.Value.Path)) },
            { "OffsetX", letterEntry.Value.OffsetX },
            { "OffsetY", letterEntry.Value.OffsetY },
            { "BaseWidth", letterEntry.Value.BaseWidth },
            { "BaseHeight", letterEntry.Value.BaseHeight },
            { "Width", letterEntry.Value.Width },
            { "Height", letterEntry.Value.Height }
        };

        static TinyObject fontEffectToTinyObject(FontEffect fontEffect)
        {
            var effectType = fontEffect.GetType();
            TinyObject cache = new()
            {
                ["Type"] = effectType.FullName
            };

            foreach (var field in effectType.GetFields())
            {
                var fieldType = field.FieldType;
                if (fieldType == typeof(FontColor))
                {
                    var color = (FontColor)field.GetValue(fontEffect);
                    cache[$"{field.Name}R"] = color.R;
                    cache[$"{field.Name}G"] = color.G;
                    cache[$"{field.Name}B"] = color.B;
                    cache[$"{field.Name}A"] = color.A;
                }
                else if (fieldType == typeof(Vector3))
                {
                    var vector = (Vector3)field.GetValue(fontEffect);
                    cache[$"{field.Name}X"] = vector.X;
                    cache[$"{field.Name}Y"] = vector.Y;
                    cache[$"{field.Name}Z"] = vector.Z;
                }
                else if (fieldType == typeof(Vector2))
                {
                    var vector = (Vector2)field.GetValue(fontEffect);
                    cache[$"{field.Name}X"] = vector.X;
                    cache[$"{field.Name}Y"] = vector.Y;
                }
                else if (fieldType == typeof(double)) cache[field.Name] = (double)field.GetValue(fontEffect);
                else if (fieldType == typeof(float)) cache[field.Name] = (float)field.GetValue(fontEffect);
                else if (fieldType == typeof(int) || fieldType.IsEnum) cache[field.Name] = (int)field.GetValue(fontEffect);
                else if (fieldType == typeof(string)) cache[field.Name] = (string)field.GetValue(fontEffect);
                else throw new InvalidDataException($"Unexpected field type {fieldType} for {field.Name} in {effectType.FullName}");
            }
            return cache;
        }
    }
}