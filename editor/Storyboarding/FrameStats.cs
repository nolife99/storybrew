using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewEditor.Storyboarding
{
    public class FrameStats
    {
        public int SpriteCount, CommandCount, EffectiveCommandCount;
        public bool IncompatibleCommands, OverlappedCommands;
        public CommandDecimal ScreenFill = 0;
    }
}