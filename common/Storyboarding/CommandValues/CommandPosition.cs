namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Util;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Vector2 = System.Numerics.Vector2;

///<summary> Base structure for movement commands.</summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandPosition
    : ICommandValue, IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
        ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
        IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
        IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
        IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    readonly Vector2d internalVec;

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.Y;

    ///<summary> Gets the square of the vector length (magnitude). </summary>
    public float LengthSquared => (float)internalVec.LengthSquared;

    ///<summary> Gets the vector length (magnitude). </summary>
    public float Length => (float)internalVec.Length;

    /// <summary> Constructs a <see cref="CommandPosition"/> from an X and Y value. </summary>
    public CommandPosition(CommandDecimal x, CommandDecimal y) => internalVec = new(x, y);

    /// <summary> Constructs a <see cref="CommandPosition"/> from a value. </summary>
    public CommandPosition(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandPosition"/> from a <see cref="Vector2"/>. </summary>
    public CommandPosition(Vector2 vector) => internalVec = new(vector.X, vector.Y);

    /// <inheritdoc/>
    public bool Equals(CommandPosition other) => internalVec == other.internalVec;

    /// <inheritdoc/>
    public override int GetHashCode() => internalVec.GetHashCode();

    TempList<char> ICommandValue.ToOsbString(ExportSettings exportSettings)
    {
        var list = TempList<char>.Create();

        using (var x =
            (exportSettings.UseFloatForMove ? (float)X : (int)double.Round(X)).ToCharArray(
                provider: exportSettings.NumberFormat)) list.AddRange(x.AsReadOnlySpan());

        list.Add(',');
        using (var y =
            (exportSettings.UseFloatForMove ? (float)Y : (int)double.Round(Y)).ToCharArray(
                provider: exportSettings.NumberFormat)) list.AddRange(y.AsReadOnlySpan());

        return list;
    }

#pragma warning disable CS1591
    public static CommandPosition operator +(CommandPosition left, CommandPosition right)
        => left.internalVec + right.internalVec;

    public static CommandPosition operator -(CommandPosition left, CommandPosition right)
        => left.internalVec - right.internalVec;

    public static CommandPosition operator -(CommandPosition pos) => -pos.internalVec;

    public static CommandPosition operator *(CommandPosition left, CommandPosition right)
        => left.internalVec * right.internalVec;

    public static CommandPosition operator *(CommandPosition left, CommandDecimal right) => left.internalVec * right;
    public static CommandPosition operator *(CommandDecimal left, CommandPosition right) => right.internalVec * left;

    public static CommandPosition operator /(CommandPosition left, CommandPosition right)
        => left.internalVec / right.internalVec;

    public static CommandPosition operator /(CommandPosition left, CommandDecimal right) => left.internalVec / right;

    public static implicit operator CommandPosition(OpenTK.Mathematics.Vector2 obj) => new(obj.X, obj.Y);

    public static implicit operator OpenTK.Mathematics.Vector2(CommandPosition obj)
        => new((float)obj.internalVec.X, (float)obj.internalVec.Y);

    public static implicit operator CommandPosition(Vector2d obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2d(CommandPosition obj) => obj.internalVec;

    public static implicit operator CommandPosition(PointF obj) => new(obj.X, obj.Y);
    public static implicit operator PointF(CommandPosition obj) => new((float)obj.internalVec.X, (float)obj.internalVec.Y);

    public static implicit operator CommandPosition(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandPosition obj) => new((float)obj.internalVec.X, (float)obj.internalVec.Y);
}