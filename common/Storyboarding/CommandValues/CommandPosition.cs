namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using Vector2 = System.Numerics.Vector2;

///<summary> Base structure for movement commands.</summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandPosition : CommandValue,
    IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
    ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
    IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    readonly Vector2 internalVec;

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.Y;

    ///<summary> Gets the square of the vector length (magnitude). </summary>
    public float LengthSquared => internalVec.LengthSquared();

    ///<summary> Gets the vector length (magnitude). </summary>
    public float Length => internalVec.Length();

    /// <summary> Constructs a <see cref="CommandPosition"/> from an X and Y value. </summary>
    public CommandPosition(CommandDecimal x, CommandDecimal y) => internalVec = new(x, y);

    /// <summary> Constructs a <see cref="CommandPosition"/> from a value. </summary>
    public CommandPosition(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandPosition"/> from a <see cref="Vector2"/>. </summary>
    public CommandPosition(Vector2 vector) => internalVec = vector;

    /// <inheritdoc/>
    public bool Equals(CommandPosition other) => internalVec == other.internalVec;

    /// <inheritdoc/>
    public override int GetHashCode() => internalVec.GetHashCode();

    ///<summary> Converts this instance to a .osb string. </summary>
    public string ToOsbString(ExportSettings exportSettings) => exportSettings.UseFloatForMove ?
        $"{internalVec.X.ToString(exportSettings.NumberFormat)},{internalVec.Y.ToString(exportSettings.NumberFormat)}" :
        $"{(int)Math.Round(internalVec.X)},{(int)Math.Round(internalVec.Y)}";

    ///<summary> Converts this instance to a string. </summary>
    public override string ToString() => internalVec.ToString();

#pragma warning disable CS1591
    public static CommandPosition operator +(CommandPosition left, CommandPosition right) => left.internalVec + right.internalVec;
    public static CommandPosition operator -(CommandPosition left, CommandPosition right) => left.internalVec - right.internalVec;
    public static CommandPosition operator -(CommandPosition pos) => -pos.internalVec;
    public static CommandPosition operator *(CommandPosition left, CommandPosition right) => left.internalVec * right.internalVec;
    public static CommandPosition operator *(CommandPosition left, CommandDecimal right) => left.internalVec * right;
    public static CommandPosition operator *(CommandDecimal left, CommandPosition right) => right.internalVec * left;
    public static CommandPosition operator /(CommandPosition left, CommandPosition right) => left.internalVec / right.internalVec;
    public static CommandPosition operator /(CommandPosition left, CommandDecimal right) => left.internalVec / right;

    public static implicit operator OpenTK.Mathematics.Vector2(CommandPosition position)
        => Unsafe.As<CommandPosition, OpenTK.Mathematics.Vector2>(ref position);

    public static implicit operator CommandPosition(OpenTK.Mathematics.Vector2 vector)
        => Unsafe.As<OpenTK.Mathematics.Vector2, CommandPosition>(ref vector);

    public static implicit operator CommandPosition(Vector2d vector) => new(vector.X, vector.Y);
    public static implicit operator Vector2d(CommandPosition position) => new(position.X, position.Y);

    public static implicit operator PointF(CommandPosition position) => position.internalVec;
    public static implicit operator CommandPosition(PointF vector) => (Vector2)vector;

    public static implicit operator Vector2(CommandPosition position) => Unsafe.As<CommandPosition, Vector2>(ref position);
    public static implicit operator CommandPosition(Vector2 vector) => Unsafe.As<Vector2, CommandPosition>(ref vector);
}