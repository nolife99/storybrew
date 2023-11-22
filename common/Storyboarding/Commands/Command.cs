using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Util;
using System.Collections.Generic;
using System.IO;

namespace StorybrewCommon.Storyboarding.Commands
{
#pragma warning disable CS1591
    public abstract class Command<TValue> : ITypedCommand<TValue>, IFragmentableCommand, IOffsetable where TValue : CommandValue
    {
        public string Identifier { get; set; }
        public OsbEasing Easing { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double Duration => EndTime - StartTime;
        public TValue StartValue { get; set; }
        public TValue EndValue { get; set; }
        public virtual bool MaintainValue => true;
        public virtual bool ExportEndValue => true;
        public bool Active => true;
        public int Cost => 1;

        protected Command(string identifier, OsbEasing easing, double startTime, double endTime, TValue startValue, TValue endValue)
        {
            Identifier = identifier;
            Easing = easing;
            StartTime = startTime;
            EndTime = endTime;
            StartValue = startValue;
            EndValue = endValue;
        }

        public void Offset(double offset)
        {
            StartTime += offset;
            EndTime += offset;
        }
        public TValue ValueAtTime(double time)
        {
            if (time < StartTime) return MaintainValue ? ValueAtProgress(0) : default;
            if (EndTime < time) return MaintainValue ? ValueAtProgress(1) : default;

            var duration = EndTime - StartTime;
            var progress = duration > 0 ? Easing.Ease((time - StartTime) / duration) : 0;
            return ValueAtProgress(progress);
        }

        public abstract TValue ValueAtProgress(double progress);
        public abstract TValue Midpoint(Command<TValue> endCommand, double progress);

        public bool IsFragmentable => StartTime == EndTime || Easing is OsbEasing.None;
        public abstract IFragmentableCommand GetFragment(double startTime, double endTime);
        public IEnumerable<int> GetNonFragmentableTimes()
        {
            if (!IsFragmentable) for (var i = 0; i < EndTime - StartTime - 1; ++i) yield return (int)(StartTime + 1 + i);
        }

        public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);
        public override int GetHashCode() => HashCode.Combine(Identifier, StartTime, EndTime, StartValue, EndValue);

        public override bool Equals(object obj) => obj is Command<TValue> other && Equals(other);
        public bool Equals(Command<TValue> obj) => Identifier == obj.Identifier && Easing == obj.Easing && 
            StartTime == obj.StartTime && EndTime == obj.EndTime && StartValue.Equals(obj.StartValue) && EndValue.Equals(obj.EndValue);

        public virtual string ToOsbString(ExportSettings exportSettings)
        {
            var startTimeString = (exportSettings.UseFloatForTime ? StartTime : (int)StartTime).ToString(exportSettings.NumberFormat);
            var endTimeString = (exportSettings.UseFloatForTime ? EndTime : (int)EndTime).ToString(exportSettings.NumberFormat);
            var startValueString = StartValue.ToOsbString(exportSettings);
            var endValueString = (ExportEndValue ? EndValue : StartValue).ToOsbString(exportSettings);

            if (startTimeString == endTimeString) endTimeString = "";

            string[] parameters =
            {
                Identifier, ((int)Easing).ToString(exportSettings.NumberFormat),
                startTimeString, endTimeString, startValueString
            };

            var result = string.Join(",", parameters);
            if (startValueString != endValueString) result += "," + endValueString;
            return result;
        }

        public virtual void WriteOsb(TextWriter writer, ExportSettings exportSettings, int indentation) => writer.WriteLine(new string(' ', indentation) + ToOsbString(exportSettings));
        public override string ToString() => ToOsbString(ExportSettings.Default);
    }
}