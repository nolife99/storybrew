using osuTK.Graphics;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Subtitles;
using System;
using System.Numerics;
using System.Drawing;
using System.IO;

namespace StorybrewScripts
{
    class Lyrics : StoryboardObjectGenerator
    {
        [Description("Path to a .sbv, .srt, .ass or .ssa file in your project's folder.\nThese can be made with a tool like aegisub.")]
        [Configurable] public string SubtitlesPath = "lyrics.srt";
        [Configurable] public float SubtitleY = 400;

        [Group("Font")]
        [Description("The name of a system font, or the path to a font relative to your project's folder.\nIt is preferable to add fonts to the project folder and use their file name rather than installing fonts.")]
        [Configurable] public string FontName = "Verdana";
        [Description("A path inside your mapset's folder where lyrics images will be generated.")]
        [Configurable] public string SpritesPath = "sb/f";
        [Description("The Size of the font.\nIncreasing the font size creates larger images.")]
        [Configurable] public int FontSize = 26;
        [Description("The Scale of the font.\nIncreasing the font scale does not creates larger images, but the result may be blurrier.")]
        [Configurable] public float FontScale = .5f;
        [Configurable] public Color4 FontColor = Color4.White;
        [Configurable] public FontStyle FontStyle = FontStyle.Regular;

        [Group("Outline")]
        [Configurable] public int OutlineThickness = 3;
        [Configurable] public Color4 OutlineColor = new(50, 50, 50, 200);

        [Group("Shadow")]
        [Configurable] public int ShadowThickness = 0;
        [Configurable] public Color4 ShadowColor = new(0, 0, 0, 100);

        [Group("Glow")]
        [Configurable] public int GlowRadius = 0;
        [Configurable] public Color4 GlowColor = new(255, 255, 255, 100);
        [Configurable] public bool GlowAdditive = true;

        [Group("Misc")]
        [Configurable] public bool PerCharacter = true;
        [Configurable] public bool TrimTransparency = true;
        [Configurable] public bool EffectsOnly = false;
        [Description("How much extra space is allocated around the text when generating it.\nShould be increased when characters look cut off.")]
        [Configurable] public Vector2 Padding = Vector2.Zero;
        [Configurable] public OsbOrigin Origin = OsbOrigin.Centre;

        protected override void Generate()
        {
            var font = LoadFont(SpritesPath, new(FontName, FontSize, FontColor, Padding, FontStyle, TrimTransparency, EffectsOnly),
                new FontGlow(GlowAdditive ? 0 : GlowRadius, 0, GlowColor), new FontOutline(OutlineThickness, OutlineColor), new FontShadow(ShadowThickness, ShadowColor));

            var subtitles = LoadSubtitles(SubtitlesPath);

            if (GlowRadius > 0 && GlowAdditive)
            {
                var glowFont = LoadFont(Path.Combine(SpritesPath, "glow"), new(FontName, FontSize, FontColor, Padding, FontStyle, TrimTransparency, true),
                    new FontGlow(GlowRadius, 0, GlowColor));
                generateLyrics(glowFont, subtitles, "glow", true);
            }
            generateLyrics(font, subtitles, "", false);
        }
        void generateLyrics(FontGenerator font, SubtitleSet subtitles, string layerName, bool additive)
        {
            var layer = GetLayer(layerName);
            if (PerCharacter) generatePerCharacter(font, subtitles, layer, additive);
            else generatePerLine(font, subtitles, layer, additive);
        }
        void generatePerLine(FontGenerator font, SubtitleSet subtitles, StoryboardLayer layer, bool additive)
        {
            foreach (var line in subtitles.Lines)
            {
                var texture = font.GetTexture(line.Text);
                var position = new Vector2(320 - texture.BaseWidth * FontScale / 2, SubtitleY) + texture.OffsetFor(Origin) * FontScale;

                var sprite = layer.CreateSprite(texture.Path, Origin, position);
                sprite.Scale(line.StartTime, FontScale);
                sprite.Fade(line.StartTime - 200, line.StartTime, 0, 1);
                sprite.Fade(line.EndTime - 200, line.EndTime, 1, 0);
                if (additive) sprite.Additive(line.StartTime - 200, line.EndTime);
            }
        }
        void generatePerCharacter(FontGenerator font, SubtitleSet subtitles, StoryboardLayer layer, bool additive)
        {
            foreach (var subtitleLine in subtitles.Lines)
            {
                var letterY = SubtitleY;
                foreach (var line in subtitleLine.Text.Split('\n'))
                {
                    var lineWidth = 0f;
                    var lineHeight = 0f;
                    foreach (var letter in line)
                    {
                        var texture = font.GetTexture(letter);
                        lineWidth += texture.BaseWidth * FontScale;
                        lineHeight = Math.Max(lineHeight, texture.BaseHeight * FontScale);
                    }

                    var letterX = 320 - lineWidth / 2;
                    foreach (var letter in line)
                    {
                        var texture = font.GetTexture(letter);
                        if (!texture.IsEmpty)
                        {
                            var position = new Vector2(letterX, letterY) + texture.OffsetFor(Origin) * FontScale;

                            var sprite = layer.CreateSprite(texture.Path, Origin, position);
                            sprite.Scale(subtitleLine.StartTime, FontScale);
                            sprite.Fade(subtitleLine.StartTime - 200, subtitleLine.StartTime, 0, 1);
                            sprite.Fade(subtitleLine.EndTime - 200, subtitleLine.EndTime, 1, 0);
                            if (additive) sprite.Additive(subtitleLine.StartTime - 200, subtitleLine.EndTime);
                        }
                        letterX += texture.BaseWidth * FontScale;
                    }
                    letterY += lineHeight;
                }
            }
        }
    }
}