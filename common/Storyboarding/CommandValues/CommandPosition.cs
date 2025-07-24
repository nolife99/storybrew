namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Util;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;

///<summary> Base structure for movement commands.</summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandPosition
    : ICommandValue, IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
        ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
        IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
        IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
        IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    internal readonly Vector2 internalVec;

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.Y;

    /// <summary> Constructs a <see cref="CommandPosition"/> from an X and Y value. </summary>
    public CommandPosition(CommandDecimal x, CommandDecimal y) => internalVec = new(x, y);

    /// <summary> Constructs a <see cref="CommandPosition"/> from a value. </summary>
    public CommandPosition(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandPosition"/> from a <see cref="Vector2"/>. </summary>
    public CommandPosition(Vector2 vector) => internalVec = vector;

    TempList<char> ICommandValue.ToOsbString(ExportSettings exportSettings) => StringHelper.Interpolate(
        exportSettings.NumberFormat,
        $"{(exportSettings.UseFloatForMove ? X : (int)float.Round(X))},{(exportSettings.UseFloatForMove ? Y : (int)float.Round(Y))}");

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
    public static implicit operator OpenTK.Mathematics.Vector2(CommandPosition obj) => new(obj.X, obj.Y);

    public static implicit operator CommandPosition(PointF obj) => new(obj.X, obj.Y);
    public static implicit operator PointF(CommandPosition obj) => new(obj.X, obj.Y);

    public static implicit operator CommandPosition(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandPosition obj) => new(obj.X, obj.Y);
}