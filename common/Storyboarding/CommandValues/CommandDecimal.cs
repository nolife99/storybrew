namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Runtime.InteropServices;

///<summary> Custom decimal handler for storyboarding. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandDecimal : CommandValue
{
    readonly double value;

#pragma warning disable CS1591
    public CommandDecimal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) this.value = 0;
        else this.value = value;
    }

    public bool Equals(CommandDecimal other) => value.Equals(other.value);

    public override int GetHashCode() => value.GetHashCode();
    public override string ToString() => ToOsbString(ExportSettings.Default);
    public string ToOsbString(ExportSettings exportSettings) => ((float)value).ToString(exportSettings.NumberFormat);

    public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => left.value - right.value;
    public static CommandDecimal operator --(CommandDecimal value) => value.value - 1;
    public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => left.value + right.value;
    public static CommandDecimal operator ++(CommandDecimal value) => value.value + 1;
    public static CommandDecimal operator *(CommandDecimal left, CommandDecimal right) => left.value * right.value;
    public static CommandDecimal operator /(CommandDecimal left, CommandDecimal right) => left.value / right.value;

    public static CommandDecimal operator -(CommandDecimal value) => -value.value;
    public static CommandDecimal operator +(CommandDecimal value) => value.value;

    public static implicit operator CommandDecimal(double value) => new(value);
    public static implicit operator double(CommandDecimal obj) => obj.value;
    public static implicit operator float(CommandDecimal obj) => (float)obj.value;
}