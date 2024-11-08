namespace StorybrewScripts;

using System.Linq;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;

internal class Background : StoryboardObjectGenerator
{
    [Configurable] public int EndTime;
    [Configurable] public float Opacity = .2f;

    [Group("Sprite"), Description("Leave empty to automatically use the map's background."), Configurable]
    
    public string SpritePath = "";

    [Group("Timing"), Configurable]
    
    public int StartTime = 0;

    protected override void Generate()
    {
        if (SpritePath == "") SpritePath = Beatmap.BackgroundPath ?? "";
        if (StartTime == EndTime) EndTime = (int)(Beatmap.HitObjects.LastOrDefault()?.EndTime ?? AudioDuration);

        var bitmap = GetMapsetBitmap(SpritePath);
        var bg = GetLayer("").CreateSprite(SpritePath);
        bg.Scale(StartTime, 480f / bitmap.Height);
        bg.Fade(StartTime - 500, StartTime, 0, Opacity);
        bg.Fade(EndTime, EndTime + 500, Opacity, 0);
    }
}