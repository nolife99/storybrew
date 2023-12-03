using System.Numerics;
using System;
using System.Drawing;

namespace StorybrewCommon.Storyboarding.CommandValues;

///<summary> Base structure for movement commands.</summary>
[Serializable] public readonly struct CommandPosition(double x, double y) : 
    CommandValue, IEquatable<CommandPosition>, 
    IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
    ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
    IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    readonly CommandDecimal x = x, y = y;

    ///<summary> Gets the X value of this instance. </summary>
    public readonly CommandDecimal X => x;

    ///<summary> Gets the Y value of this instance. </summary>
    public readonly CommandDecimal Y => y;

    ///<summary> Gets the square of the vector length (magnitude). </summary>
    public readonly float LengthSquared => x * x + y * y;

    ///<summary> Gets the vector length (magnitude). </summary>
    public readonly float Length => MathF.Sqrt(x * x + y * y);

    ///<inheritdoc/>
    public readonly bool Equals(CommandPosition other) => x.Equals(other.x) && y.Equals(other.y);

    ///<inheritdoc/>
    public override readonly bool Equals(object obj) => obj is CommandPosition position && Equals(position);

    ///<inheritdoc/>
    public override readonly int GetHashCode() => x.GetHashCode() ^ y.GetHashCode();

    ///<summary> Converts this instance to a .osb string. </summary>
    public readonly string ToOsbString(ExportSettings exportSettings) => exportSettings.UseFloatForMove ? $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}" : $"{(int)Math.Round(X)},{(int)Math.Round(Y)}";
    
    ///<summary> Converts this instance to a string. </summary>
    public override readonly string ToString() => $"<{X}, {Y}>";

    ///<summary> Returns the distance between this instance and point <paramref name="obj"/> on the Cartesian plane. </summary>
    public readonly float DistanceFrom(object obj)
    {
        var vector = this - (CommandPosition)obj;
        return MathF.Sqrt(vector.x * vector.x + vector.y * vector.y);
    }

#pragma warning disable CS1591
    public static CommandPosition operator +(CommandPosition left, CommandPosition right) => new(left.x + right.x, left.y + right.y);
    public static CommandPosition operator -(CommandPosition left, CommandPosition right) => new(left.x - right.x, left.y - right.y);
    public static CommandPosition operator -(CommandPosition pos) => new(-pos.x, -pos.y);
    public static CommandPosition operator *(CommandPosition left, CommandPosition right) => new(left.x * right.x, left.y * right.y);
    public static CommandPosition operator *(CommandPosition left, double right) => new(left.x * right, left.y * right);
    public static CommandPosition operator *(double left, CommandPosition right) => right * left;
    public static CommandPosition operator /(CommandPosition left, CommandPosition right) => new(left.x / right.x, left.y / right.y);
    public static CommandPosition operator /(CommandPosition left, double right) => new(left.x / right, left.y / right);
    public static bool operator ==(CommandPosition left, CommandPosition right) => left.Equals(right);
    public static bool operator !=(CommandPosition left, CommandPosition right) => !left.Equals(right);
    public static implicit operator osuTK.Vector2(CommandPosition position) => new(position.x, position.y);
    public static implicit operator osuTK.Vector2d(CommandPosition position) => new(position.x, position.y);
    public static implicit operator Vector2(CommandPosition position) => new(position.x, position.y);
    public static implicit operator PointF(CommandPosition position) => new(position.x, position.y);
    public static implicit operator CommandScale(CommandPosition position) => new(position.x, position.y);
    public static implicit operator CommandPosition(osuTK.Vector2 vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(osuTK.Vector2d vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(Vector2 vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(PointF vector) => new(vector.X, vector.Y);
    public static implicit operator CommandPosition(CommandScale position) => new(position.X, position.Y);
}