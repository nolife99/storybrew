using System.Numerics;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace StorybrewCommon.Storyboarding.CommandValues;

///<summary> Base structure for movement commands.</summary>
[Serializable] public readonly struct CommandPosition : 
    CommandValue, IEquatable<CommandPosition>, 
    IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
    ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
    IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    readonly Vector2 internalVec;

    ///<summary> Gets the X value of this instance. </summary>
    public readonly CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public readonly CommandDecimal Y => internalVec.Y;

    ///<summary> Gets the square of the vector length (magnitude). </summary>
    public readonly float LengthSquared => internalVec.LengthSquared();

    ///<summary> Gets the vector length (magnitude). </summary>
    public readonly float Length => internalVec.Length();

    ///<summary> Constructs a <see cref="CommandPosition"/> from an X and Y value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandPosition(CommandDecimal x, CommandDecimal y) => internalVec = new(x, y);

    ///<summary> Constructs a <see cref="CommandPosition"/> from a value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandPosition(CommandDecimal value) : this(value, value) { }

    ///<summary> Constructs a <see cref="CommandPosition"/> from a <see cref="Vector2"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandPosition(Vector2 vector) => internalVec = vector;

    ///<inheritdoc/>
    public readonly bool Equals(CommandPosition other) => internalVec == other.internalVec;

    ///<inheritdoc/>
    public override readonly bool Equals(object obj) => obj is CommandPosition position && Equals(position);

    ///<inheritdoc/>
    public override readonly int GetHashCode() => internalVec.GetHashCode();

    ///<summary> Converts this instance to a .osb string. </summary>
    public readonly string ToOsbString(ExportSettings exportSettings) => exportSettings.UseFloatForMove ? $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}" : $"{(int)Math.Round(X)},{(int)Math.Round(Y)}";
    
    ///<summary> Converts this instance to a string. </summary>
    public override readonly string ToString() => internalVec.ToString();

#pragma warning disable CS1591
    public static CommandPosition operator +(CommandPosition left, CommandPosition right) => left.internalVec + right.internalVec;
    public static CommandPosition operator -(CommandPosition left, CommandPosition right) => left.internalVec - right.internalVec;
    public static CommandPosition operator -(CommandPosition pos) => -pos.internalVec;
    public static CommandPosition operator *(CommandPosition left, CommandPosition right) => left.internalVec * right.internalVec;
    public static CommandPosition operator *(CommandPosition left, double right) => new(left.internalVec.X * right, left.internalVec.Y * right);
    public static CommandPosition operator *(double left, CommandPosition right) => new(right.internalVec.X * left, right.internalVec.Y * left);
    public static CommandPosition operator /(CommandPosition left, CommandPosition right) => left.internalVec / right.internalVec;
    public static CommandPosition operator /(CommandPosition left, double right) => new(left.internalVec.X / right, left.internalVec.Y / right);
    public static bool operator ==(CommandPosition left, CommandPosition right) => left.Equals(right);
    public static bool operator !=(CommandPosition left, CommandPosition right) => !left.Equals(right);
    public static implicit operator osuTK.Vector2(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator osuTK.Vector2d(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator Vector2(CommandPosition position) => position.internalVec;
    public static implicit operator PointF(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator CommandPosition(osuTK.Vector2 vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(osuTK.Vector2d vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(Vector2 vector) => new(vector);
    public static implicit operator CommandPosition(PointF vector) => new(vector.X, vector.Y);
}