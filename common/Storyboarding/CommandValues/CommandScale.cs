using OpenTK;
using System;
using System.Drawing;

namespace StorybrewCommon.Storyboarding.CommandValues
{
    ///<summary> Base struct for vector scale commands. Alternative for <see cref="Vector2"/>. </summary>
    [Serializable] public struct CommandScale : CommandValue, IEquatable<CommandScale>
    {
        ///<summary> Represents a scale in which all values are 1 (one). </summary>
        public static CommandScale One = new CommandScale(1, 1);

        readonly CommandDecimal x, y;

        ///<summary> Gets the X value of this instance. </summary>
        public CommandDecimal X => x;

        ///<summary> Gets the Y value of this instance. </summary>
        public CommandDecimal Y => y;

        ///<summary> Constructs a <see cref="CommandScale"/> from X and Y values. </summary>
        public CommandScale(CommandDecimal x, CommandDecimal y)
        {
            this.x = x;
            this.y = y;
        }

        ///<summary> Constructs a <see cref="CommandScale"/> from a value. </summary>
        public CommandScale(CommandDecimal value) : this(value, value) { }

        ///<summary> Constructs a <see cref="CommandScale"/> from a <see cref="Vector2"/>. </summary>
        public CommandScale(Vector2 vector) : this(vector.X, vector.Y) { }

        ///<summary> Constructs a <see cref="CommandScale"/> from a <see cref="System.Numerics.Vector2"/>. </summary>
        public CommandScale(System.Numerics.Vector2 vector) : this(vector.X, vector.Y) { }

        ///<inheritdoc/>
        public bool Equals(CommandScale other) => x.Equals(other.x) && y.Equals(other.y);

        ///<inheritdoc/>
        public override bool Equals(object obj) => obj is CommandScale scale && Equals(scale);

        ///<inheritdoc/>
        public override int GetHashCode() => ((System.Numerics.Vector2)this).GetHashCode();

        ///<summary> Converts this instance to a .osb string. </summary>
        public string ToOsbString(ExportSettings exportSettings) => $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}";

        ///<summary> Converts this instance to a string. </summary>
        public override string ToString() => ((Vector2)this).ToString();

        ///<summary> Returns the distance between this instance and point <paramref name="obj"/> on the Cartesian plane. </summary>
        public float DistanceFrom(object obj) => Vector2.Distance(this, (Vector2)obj);

#pragma warning disable CS1591
        public static CommandScale operator +(CommandScale left, CommandScale right) => new CommandScale(left.x + right.x, left.y + right.y);
        public static CommandScale operator -(CommandScale left, CommandScale right) => new CommandScale(left.x - right.x, left.y - right.y);
        public static CommandScale operator *(CommandScale left, CommandScale right) => new CommandScale(left.x * right.x, left.y * right.y);
        public static CommandScale operator *(CommandScale left, double right) => new CommandScale(left.x * right, left.y * right);
        public static CommandScale operator *(double left, CommandScale right) => right * left;
        public static CommandScale operator /(CommandScale left, double right) => new CommandScale(left.x / right, left.y / right);
        public static bool operator ==(CommandScale left, CommandScale right) => left.Equals(right);
        public static bool operator !=(CommandScale left, CommandScale right) => !left.Equals(right);
        public static implicit operator CommandScale(Vector2 vector) => new CommandScale(vector);
        public static implicit operator CommandScale(SizeF vector) => new CommandScale(vector.Width, vector.Height);
        public static implicit operator CommandScale(System.Numerics.Vector2 vector) => new CommandScale(vector);
        public static implicit operator Vector2(CommandScale obj) => new Vector2(obj.x, obj.y);
        public static implicit operator SizeF(CommandScale vector) => new SizeF(vector.x, vector.y);
        public static implicit operator System.Numerics.Vector2(CommandScale obj) => new System.Numerics.Vector2(obj.x, obj.y);
    }
}