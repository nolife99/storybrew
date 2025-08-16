namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using BrewLib.Util;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;

/// <summary> Base structure for movement commands. </summary>
public readonly record struct CommandPosition : ICommandValue<CommandPosition>,
    IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
    IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
    IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    internal readonly Vector128<double> internalVec;

    /// <summary> Constructs a <see cref="CommandPosition"/> from an X and Y value. </summary>
    public CommandPosition(CommandDecimal x, CommandDecimal y) => internalVec = Vector128.Create(x, y);

    /// <summary> Constructs a <see cref="CommandPosition"/> from a value. </summary>
    public CommandPosition(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandPosition"/> from a <see cref="Vector2"/>. </summary>
    public CommandPosition(Vector2 vector) => internalVec = Vector128.Create(vector.X, vector.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CommandPosition(Vector128<double> vec) => internalVec = vec;

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.GetLower().ToScalar();

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.GetUpper().ToScalar();

    TempList<char> ICommandValue<CommandPosition>.ToOsbString(ExportSettings exportSettings)
        => StringHelper.Interpolate(exportSettings.NumberFormat,
            $"{(exportSettings.UseFloatForMove ? X : (int)float.Round(X))},{(exportSettings.UseFloatForMove ? Y : (int)float.Round(Y))}");

    /// <summary> Performs a linear interpolation between two vectors based on the given weighting. </summary>
    public static CommandPosition Lerp(CommandPosition a, CommandPosition b, float t)
        => new(Vector128.Lerp(a.internalVec, b.internalVec, Vector128.Create((double)t)));

#pragma warning disable CS1591
    public static CommandPosition operator +(CommandPosition left, CommandPosition right)
        => new(left.internalVec + right.internalVec);

    public static CommandPosition operator -(CommandPosition left, CommandPosition right)
        => new(left.internalVec - right.internalVec);

    public static CommandPosition operator -(CommandPosition pos) => new(-pos.internalVec);

    public static CommandPosition operator *(CommandPosition left, CommandPosition right)
        => new(left.internalVec * right.internalVec);

    public static CommandPosition operator *(CommandPosition left, CommandDecimal right)
        => new(left.internalVec * right);

    public static CommandPosition operator *(CommandDecimal left, CommandPosition right)
        => new(right.internalVec * left);

    public static CommandPosition operator /(CommandPosition left, CommandPosition right)
        => new(left.internalVec / right.internalVec);

    public static CommandPosition operator /(CommandPosition left, CommandDecimal right)
        => new(left.internalVec / right);

    public static implicit operator CommandPosition(OpenTK.Mathematics.Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator OpenTK.Mathematics.Vector2(CommandPosition obj) => new(obj.X, obj.Y);

    public static implicit operator CommandPosition(PointF obj) => new(obj.X, obj.Y);
    public static implicit operator PointF(CommandPosition obj) => new(obj.X, obj.Y);

    public static implicit operator CommandPosition(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandPosition obj) => new(obj.X, obj.Y);
}