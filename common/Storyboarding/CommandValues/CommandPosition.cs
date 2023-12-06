using System.Numerics;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StorybrewCommon.Storyboarding.CommandValues;

///<summary> Base structure for movement commands.</summary>
[Serializable] public readonly unsafe struct CommandPosition(CommandDecimal x, CommandDecimal y) : 
    CommandValue, IEquatable<CommandPosition>, 
    IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
    ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
    IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    readonly Vector2 internalVec = new(x, y);

    ///<summary> Gets the X value of this instance. </summary>
    public readonly CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public readonly CommandDecimal Y => internalVec.Y;

    ///<summary> Gets the square of the vector length (magnitude). </summary>
    public readonly float LengthSquared => internalVec.LengthSquared();

    ///<summary> Gets the vector length (magnitude). </summary>
    public readonly float Length => internalVec.Length();

    ///<inheritdoc/>
    public readonly bool Equals(CommandPosition other) => internalVec == other.internalVec;

    ///<inheritdoc/>
    public override readonly bool Equals(object obj) => obj is CommandPosition position && Equals(position);

    ///<inheritdoc/>
    public override readonly int GetHashCode() => internalVec.GetHashCode();

    ///<summary> Converts this instance to a .osb string. </summary>
    public readonly string ToOsbString(ExportSettings exportSettings) => exportSettings.UseFloatForMove ? $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}" : $"{(int)Math.Round(X)},{(int)Math.Round(Y)}";
    
    ///<summary> Converts this instance to a string. </summary>
    public override readonly string ToString() => $"<{X}, {Y}>";

#pragma warning disable CS1591
    public static CommandPosition operator +(CommandPosition left, CommandPosition right) => left.internalVec + right.internalVec;
    public static CommandPosition operator -(CommandPosition left, CommandPosition right) => left.internalVec - right.internalVec;
    public static CommandPosition operator -(CommandPosition pos) => -pos.internalVec;
    public static CommandPosition operator *(CommandPosition left, CommandPosition right) => left.internalVec * right.internalVec;
    public static CommandPosition operator *(CommandPosition left, double right) => new(left.internalVec.X * right, left.internalVec.Y * right);
    public static CommandPosition operator *(double left, CommandPosition right) => right * left;
    public static CommandPosition operator /(CommandPosition left, CommandPosition right) => left.internalVec / right.internalVec;
    public static CommandPosition operator /(CommandPosition left, double right) => new(left.internalVec.X / right, left.internalVec.Y / right);
    public static bool operator ==(CommandPosition left, CommandPosition right) => left.Equals(right);
    public static bool operator !=(CommandPosition left, CommandPosition right) => !left.Equals(right);
    public static implicit operator osuTK.Vector2(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator osuTK.Vector2d(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator Vector2(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator PointF(CommandPosition position) => new(position.X, position.Y);
    public static implicit operator CommandPosition(osuTK.Vector2 vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(osuTK.Vector2d vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(Vector2 vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(PointF vector) => new(vector.X, vector.Y);
}