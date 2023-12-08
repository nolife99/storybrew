using System;
using System.Drawing;
using System.Numerics;

namespace StorybrewCommon.Storyboarding.CommandValues;

///<summary> Base structure for scale commands. </summary>
[Serializable] public readonly struct CommandScale(CommandDecimal x, CommandDecimal y) : 
    CommandValue, IEquatable<CommandScale>,
    IAdditionOperators<CommandScale, CommandScale, CommandScale>,
    ISubtractionOperators<CommandScale, CommandScale, CommandScale>,
    IMultiplyOperators<CommandScale, CommandScale, CommandScale>,
    IDivisionOperators<CommandScale, CommandScale, CommandScale>
{
    readonly Vector2 internalVec = new(x, y);

    ///<summary> Represents a scale vector in which all values are 1 (one). </summary>
    public static readonly CommandScale One = new(1, 1);

    ///<summary> Gets the X value of this instance. </summary>
    public readonly CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public readonly CommandDecimal Y => internalVec.Y;

    ///<summary> Constructs a <see cref="CommandScale"/> from a value. </summary>
    public CommandScale(CommandDecimal value) : this(value, value) { }

    ///<summary> Constructs a <see cref="CommandScale"/> from a <see cref="osuTK.Vector2"/>. </summary>
    public CommandScale(osuTK.Vector2 vector) : this(vector.X, vector.Y) { }

    ///<summary> Constructs a <see cref="CommandScale"/> from a <see cref="Vector2"/>. </summary>
    public CommandScale(Vector2 vector) : this(vector.X, vector.Y) { }

    ///<inheritdoc/>
    public readonly bool Equals(CommandScale other) => internalVec == other.internalVec;

    ///<inheritdoc/>
    public override readonly bool Equals(object obj) => obj is CommandScale scale && Equals(scale);

    ///<inheritdoc/>
    public override readonly int GetHashCode() => internalVec.GetHashCode();

    ///<summary> Converts this instance to a .osb string. </summary>
    public readonly string ToOsbString(ExportSettings exportSettings) => $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}";

    ///<summary> Converts this instance to a string. </summary>
    public override readonly string ToString() => ToOsbString(ExportSettings.Default);

#pragma warning disable CS1591
    public static CommandScale operator +(CommandScale left, CommandScale right) => left.internalVec + right.internalVec;
    public static CommandScale operator -(CommandScale left, CommandScale right) => left.internalVec - right.internalVec;
    public static CommandScale operator *(CommandScale left, CommandScale right) => left.internalVec * right.internalVec;
    public static CommandScale operator *(CommandScale left, double right) => new(left.internalVec.X * right, left.internalVec.Y * right);
    public static CommandScale operator /(CommandScale left, CommandScale right) => left.internalVec / right.internalVec;
    public static CommandScale operator /(CommandScale left, double right) => new(left.internalVec.X / right, left.internalVec.Y / right);
    public static bool operator ==(CommandScale left, CommandScale right) => left.Equals(right);
    public static bool operator !=(CommandScale left, CommandScale right) => !left.Equals(right);
    public static implicit operator CommandScale(osuTK.Vector2 vector) => new(vector);
    public static implicit operator CommandScale(osuTK.Vector2d vector) => new(vector.X, vector.Y);
    public static implicit operator CommandScale(SizeF vector) => new(vector.Width, vector.Height);
    public static implicit operator CommandScale(Vector2 vector) => new(vector);
    public static implicit operator CommandScale(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator osuTK.Vector2(CommandScale obj) => new(obj.X, obj.Y);
    public static implicit operator osuTK.Vector2d(CommandScale obj) => new(obj.X, obj.Y);
    public static implicit operator SizeF(CommandScale vector) => new(vector.X, vector.Y);
    public static implicit operator Vector2(CommandScale obj) => new(obj.X, obj.Y);
    public static implicit operator CommandPosition(CommandScale position) => new(position.X, position.Y);
}