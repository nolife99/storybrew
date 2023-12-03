using osuTK;

namespace BrewLib.Util.Compression
{
    public sealed class LosslessInputSettings(int level, string args = "")
    {
        public readonly string CustomInputArgs = args;
        public readonly int OptimizationLevel = MathHelper.Clamp(level, 0, 7);
    }
    public sealed class LossyInputSettings(int min, int max, int speed, string args = "")
    {
        public readonly string CustomInputArgs = args;
        public readonly int MinQuality = MathHelper.Clamp(min, 0, 100), MaxQuality = MathHelper.Clamp(max, 0, 100), Speed = MathHelper.Clamp(speed, 1, 11);
    }
}