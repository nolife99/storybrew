using System;

namespace StorybrewCommon.Storyboarding.CommandValues
{
#pragma warning disable CS1591
    [Serializable] public readonly struct CommandParameter : CommandValue
    {
        public static readonly CommandParameter None = new(ParameterType.None);
        public static readonly CommandParameter FlipHorizontal = new(ParameterType.FlipHorizontal);
        public static readonly CommandParameter FlipVertical = new(ParameterType.FlipVertical);
        public static readonly CommandParameter AdditiveBlending = new(ParameterType.AdditiveBlending);

        public readonly ParameterType Type;

        CommandParameter(ParameterType type) => Type = type;
        public string ToOsbString(ExportSettings exportSettings)
        {
            switch (Type)
            {
                case ParameterType.FlipHorizontal: return "H";
                case ParameterType.FlipVertical: return "V";
                case ParameterType.AdditiveBlending: return "A";
                default: throw new InvalidOperationException($"Parameter command cannot be None.");
            }
        }
        public override string ToString() => ToOsbString(ExportSettings.Default);

        public float DistanceFrom(object obj) => ((CommandParameter)obj).Type != Type ? 1 : 0;
        public bool Equals(CommandParameter obj) => Type == obj.Type;
        public override bool Equals(object obj) => obj is CommandParameter parameter && Equals(parameter);
        public override int GetHashCode() => ToOsbString(ExportSettings.Default)[0].GetHashCode();

        public static bool operator ==(CommandParameter left, CommandParameter right) => left.Equals(right);
        public static bool operator !=(CommandParameter left, CommandParameter right) => !left.Equals(right);
        public static implicit operator bool(CommandParameter obj) => obj.Type != ParameterType.None;
    }
}