using System;

namespace StorybrewCommon.Storyboarding.CommandValues
{
    ///<summary> Custom decimal handler for storyboarding. </summary>
    [Serializable] public readonly struct CommandDecimal : CommandValue, IEquatable<CommandDecimal>
    {
        readonly double value;

#pragma warning disable CS1591
        public CommandDecimal(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                this.value = 0;
                return;
            }
            this.value = value;
        }

        public bool Equals(CommandDecimal other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is CommandDecimal deci && Equals(deci);

        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => ToOsbString(ExportSettings.Default);
        public float DistanceFrom(object obj) => (float)Math.Abs(value - ((CommandDecimal)obj).value);
        public string ToOsbString(ExportSettings exportSettings) => ((float)value).ToString(exportSettings.NumberFormat);

        public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => left.value - right.value;
        public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => left.value + right.value;
        public static bool operator ==(CommandDecimal left, CommandDecimal right) => left.Equals(right);
        public static bool operator !=(CommandDecimal left, CommandDecimal right) => !left.Equals(right);
        public static implicit operator CommandDecimal(double value) => new(value);
        public static implicit operator double(CommandDecimal obj) => obj.value;
        public static implicit operator float(CommandDecimal obj) => (float)obj.value;
    }
}