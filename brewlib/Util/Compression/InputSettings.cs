using OpenTK;

namespace BrewLib.Util.Compression
{
    public sealed class LosslessInputSettings
    {
        public readonly string CustomInputArgs;
        public readonly int OptimizationLevel;

        public LosslessInputSettings(int level, string args = "")
        {
            OptimizationLevel = MathHelper.Clamp(level, 0, 7);
            CustomInputArgs = args;
        }
    }
    public sealed class LossyInputSettings
    {
        public readonly string CustomInputArgs;
        public readonly int MinQuality, MaxQuality, Speed;

        public LossyInputSettings(int min, int max, int speed, string args = "")
        {
            MinQuality = MathHelper.Clamp(min, 0, 100);
            MaxQuality = MathHelper.Clamp(max, 0, 100);
            Speed = MathHelper.Clamp(speed, 1, 11);
            CustomInputArgs = args;
        }
    }
}