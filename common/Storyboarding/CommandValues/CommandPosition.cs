using System.Numerics;
using System;
using System.Drawing;

namespace StorybrewCommon.Storyboarding.CommandValues
{
    ///<summary> Base struct for movement commands.</summary>
    [Serializable] public struct CommandPosition : CommandValue, IEquatable<CommandPosition>
    {
        readonly CommandDecimal x, y;

        ///<summary> Gets the X value of this instance. </summary>
        public CommandDecimal X => x;

        ///<summary> Gets the Y value of this instance. </summary>
        public CommandDecimal Y => y;

        ///<summary> Gets the square of the vector length (magnitude). </summary>
        public float LengthSquared => x * x + y * y;

        ///<summary> Gets the vector length (magnitude). </summary>
        public float Length => (float)Math.Sqrt(x * x + y * y);

        ///<summary> Constructs a <see cref="CommandPosition"/> from X and Y values. </summary>
        public CommandPosition(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        ///<inheritdoc/>
        public bool Equals(CommandPosition other) => x.Equals(other.x) && y.Equals(other.y);

        ///<inheritdoc/>
        public override bool Equals(object obj) => obj is CommandPosition position && Equals(position);

        ///<inheritdoc/>
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();

        ///<summary> Converts this instance to a .osb string. </summary>
        public string ToOsbString(ExportSettings exportSettings) => exportSettings.UseFloatForMove ? $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}" : $"{(int)Math.Round(X)},{(int)Math.Round(Y)}";
        
        ///<summary> Converts this instance to a string. </summary>
        public override string ToString() => $"<{X}, {Y}>";

        ///<summary> Returns the distance between this instance and point <paramref name="obj"/> on the Cartesian plane. </summary>
        public float DistanceFrom(object obj)
        {
            var vector = this - (CommandPosition)obj;
            return (float)Math.Sqrt(vector.x * vector.x + vector.y * vector.y);
        }

#pragma warning disable CS1591
        public static CommandPosition operator +(CommandPosition left, CommandPosition right) => new CommandPosition(left.x + right.x, left.y + right.y);
        public static CommandPosition operator -(CommandPosition left, CommandPosition right) => new CommandPosition(left.x - right.x, left.y - right.y);
        public static CommandPosition operator -(CommandPosition pos) => new CommandPosition(-pos.x, -pos.y);
        public static CommandPosition operator *(CommandPosition left, CommandPosition right) => new CommandPosition(left.x * right.x, left.y * right.y);
        public static CommandPosition operator *(CommandPosition left, double right) => new CommandPosition(left.x * right, left.y * right);
        public static CommandPosition operator *(double left, CommandPosition right) => right * left;
        public static CommandPosition operator /(CommandPosition left, double right) => new CommandPosition(left.x / right, left.y / right);
        public static bool operator ==(CommandPosition left, CommandPosition right) => left.Equals(right);
        public static bool operator !=(CommandPosition left, CommandPosition right) => !left.Equals(right);
        public static implicit operator OpenTK.Vector2(CommandPosition position) => new OpenTK.Vector2(position.x, position.Y);
        public static implicit operator Vector2(CommandPosition position) => new Vector2(position.x, position.Y);
        public static implicit operator PointF(CommandPosition position) => new PointF(position.x, position.Y);
        public static implicit operator CommandPosition(OpenTK.Vector2 vector) => new CommandPosition(vector.X, vector.Y);
        public static implicit operator CommandPosition(Vector2 vector) => new CommandPosition(vector.X, vector.Y);
        public static implicit operator CommandPosition(PointF vector) => new CommandPosition(vector.X, vector.Y);
    }
}