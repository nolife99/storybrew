using System.Linq;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;

namespace StorybrewScripts;

class Background : StoryboardObjectGenerator
{
    [Group("Timing")]
    [Configurable] public int StartTime = 0;
    [Configurable] public int EndTime = 0;

    [Group("Sprite")]
    [Description("Leave empty to automatically use the map's background.")]
    [Configurable] public string SpritePath = "";
    [Configurable] public float Opacity = .2f;

    protected override void Generate()
    {
        if (SpritePath == "") SpritePath = Beatmap.BackgroundPath ?? "";
        if (StartTime == EndTime) EndTime = (int)(Beatmap.HitObjects.LastOrDefault()?.EndTime ?? AudioDuration);

        var bitmap = GetMapsetBitmap(SpritePath);
        var bg = GetLayer("").CreateSprite(SpritePath, OsbOrigin.Centre);
        bg.Scale(StartTime, 480f / bitmap.Height);
        bg.Fade(StartTime - 500, StartTime, 0, Opacity);
        bg.Fade(EndTime, EndTime + 500, Opacity, 0);
    }
}