using osuTK;
using System;
using System.Drawing;

namespace StorybrewCommon.Storyboarding.CommandValues
{
    ///<summary> Base struct for vector scale commands. Alternative for <see cref="Vector2"/>. </summary>
    ///<remarks> Constructs a <see cref="CommandScale"/> from X and Y values. </remarks>
    [Serializable] public struct CommandScale(CommandDecimal x, CommandDecimal y) : CommandValue, IEquatable<CommandScale>
    {
        ///<summary> Represents a scale in which all values are 1 (one). </summary>
        public static CommandScale One = new(1, 1);

        readonly CommandDecimal x = x, y = y;

        ///<summary> Gets the X value of this instance. </summary>
        public readonly CommandDecimal X => x;

        ///<summary> Gets the Y value of this instance. </summary>
        public readonly CommandDecimal Y => y;

        ///<summary> Constructs a <see cref="CommandScale"/> from a value. </summary>
        public CommandScale(CommandDecimal value) : this(value, value) { }

        ///<summary> Constructs a <see cref="CommandScale"/> from a <see cref="Vector2"/>. </summary>
        public CommandScale(Vector2 vector) : this(vector.X, vector.Y) { }

        ///<summary> Constructs a <see cref="CommandScale"/> from a <see cref="System.Numerics.Vector2"/>. </summary>
        public CommandScale(System.Numerics.Vector2 vector) : this(vector.X, vector.Y) { }

        ///<inheritdoc/>
        public readonly bool Equals(CommandScale other) => x.Equals(other.x) && y.Equals(other.y);

        ///<inheritdoc/>
        public override bool Equals(object obj) => obj is CommandScale scale && Equals(scale);

        ///<inheritdoc/>
        public override readonly int GetHashCode() => ((System.Numerics.Vector2)this).GetHashCode();

        ///<summary> Converts this instance to a .osb string. </summary>
        public string ToOsbString(ExportSettings exportSettings) => $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}";

        ///<summary> Converts this instance to a string. </summary>
        public override readonly string ToString() => ((Vector2)this).ToString();

        ///<summary> Returns the distance between this instance and point <paramref name="obj"/> on the Cartesian plane. </summary>
        public readonly float DistanceFrom(object obj) => Vector2.Distance(this, (Vector2)obj);

#pragma warning disable CS1591
        public static CommandScale operator +(CommandScale left, CommandScale right) => new(left.x + right.x, left.y + right.y);
        public static CommandScale operator -(CommandScale left, CommandScale right) => new(left.x - right.x, left.y - right.y);
        public static CommandScale operator *(CommandScale left, CommandScale right) => new(left.x * right.x, left.y * right.y);
        public static CommandScale operator *(CommandScale left, double right) => new(left.x * right, left.y * right);
        public static CommandScale operator *(double left, CommandScale right) => right * left;
        public static CommandScale operator /(CommandScale left, double right) => new(left.x / right, left.y / right);
        public static bool operator ==(CommandScale left, CommandScale right) => left.Equals(right);
        public static bool operator !=(CommandScale left, CommandScale right) => !left.Equals(right);
        public static implicit operator CommandScale(Vector2 vector) => new(vector);
        public static implicit operator CommandScale(SizeF vector) => new(vector.Width, vector.Height);
        public static implicit operator CommandScale(System.Numerics.Vector2 vector) => new(vector);
        public static implicit operator Vector2(CommandScale obj) => new(obj.x, obj.y);
        public static implicit operator SizeF(CommandScale vector) => new(vector.x, vector.y);
        public static implicit operator System.Numerics.Vector2(CommandScale obj) => new(obj.x, obj.y);
    }
}