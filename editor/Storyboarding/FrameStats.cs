using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewEditor.Storyboarding
{
    public class FrameStats
    {
        public int SpriteCount = 0, CommandCount = 0, EffectiveCommandCount = 0;
        public bool IncompatibleCommands = false, OverlappedCommands = false;
        public CommandDecimal ScreenFill = 0;
    }
}