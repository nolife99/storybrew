namespace StorybrewScripts;

using System.Numerics;
using StorybrewCommon.Animations;
using StorybrewCommon.Mapset;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.Util;

internal class HitObjectHighlight : StoryboardObjectGenerator
{
    [Configurable] public bool Additive = true;
    [Configurable] public int BeatDivisor = 480;
    [Configurable] public int EndTime = 0;
    [Configurable] public float FadeDuration = 1000;

    [Group("Sprite"), Configurable] public string SpritePath = "sb/glow.png";

    [Configurable] public float SpriteScale = 1;

    [Group("Timing"), Configurable] public int StartTime = 0;

    protected override void Generate()
    {
        using OsbSpritePool pool = new(GetLayer(""), SpritePath, Additive);
        foreach (var hitobject in Beatmap.HitObjects)
        {
            if ((StartTime != 0 || EndTime != 0) &&
                (hitobject.StartTime < StartTime - 5 || EndTime - 5 <= hitobject.StartTime)) continue;

            var hSprite = pool.Get(hitobject.StartTime, hitobject.EndTime + FadeDuration);

            var pos = hitobject.Position + hitobject.StackOffset;
            if (hSprite.PositionAt(hitobject.StartTime) != pos && hitobject is not OsuSlider)
                hSprite.Move(hitobject.StartTime, pos + hitobject.StackOffset);

            hSprite.Scale(OsbEasing.In, hitobject.StartTime, hitobject.EndTime + FadeDuration, SpriteScale, SpriteScale / 5);
            hSprite.Fade(OsbEasing.In, hitobject.StartTime, hitobject.EndTime + FadeDuration, 1, 0);
            if (hSprite.ColorAt(hitobject.StartTime) != hitobject.Color) hSprite.Color(hitobject.StartTime, hitobject.Color);

            if (hitobject is OsuSlider)
            {
                KeyframedValue<Vector2> keyframe = [];
                var timestep = Beatmap.GetTimingPointAt(hitobject.StartTime).BeatDuration / BeatDivisor;
                var startTime = hitobject.StartTime;

                while (true)
                {
                    var endTime = startTime + timestep;

                    var complete = hitobject.EndTime - startTime < 5;
                    if (complete) endTime = hitobject.EndTime;

                    var startPosition = hitobject.PositionAtTime(startTime);
                    keyframe.Add(startTime, startPosition + hitobject.StackOffset);

                    if (complete) break;
                    startTime += timestep;
                }

                keyframe.Simplify2dKeyframes(1, v => v);
                keyframe.ForEachPair((sTime, eTime) => hSprite.Move(sTime.Time, eTime.Time, sTime.Value, eTime.Value));
            }
        }
    }
}