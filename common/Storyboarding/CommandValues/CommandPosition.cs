namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BrewLib.Util;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

///<summary> Base structure for movement commands.</summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandPosition
    : ICommandValue, IAdditionOperators<CommandPosition, CommandPosition, CommandPosition>,
        ISubtractionOperators<CommandPosition, CommandPosition, CommandPosition>,
        IMultiplyOperators<CommandPosition, CommandPosition, CommandPosition>,
        IDivisionOperators<CommandPosition, CommandPosition, CommandPosition>,
        IUnaryNegationOperators<CommandPosition, CommandPosition>
{
    internal readonly Vector128<double> internalVec;

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.GetLower().ToScalar();

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.GetUpper().ToScalar();

    /// <summary> Constructs a <see cref="CommandPosition"/> from an X and Y value. </summary>
    public CommandPosition(CommandDecimal x, CommandDecimal y) => internalVec = Vector128.Create(x, y);

    /// <summary> Constructs a <see cref="CommandPosition"/> from a value. </summary>
    public CommandPosition(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandPosition"/> from a <see cref="Vector2"/>. </summary>
    public CommandPosition(Vector2 vector) => internalVec = Vector128.Create(vector.X, vector.Y);

    CommandPosition(ref readonly Vector128<double> vec) => internalVec = vec;

    /// <inheritdoc/>
    public bool Equals(CommandPosition other) => internalVec == other.internalVec;

    /// <inheritdoc/>
    public override int GetHashCode() => internalVec.GetHashCode();

    TempList<char> ICommandValue.ToOsbString(ExportSettings exportSettings)
    {
        var list = TempList.Create<char>();

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
    public static implicit operator OpenTK.Mathematics.Vector2(CommandPosition obj) => new(obj.X, obj.Y);

    public static implicit operator CommandPosition(Vector128<double> obj) => new(in obj);
    public static implicit operator Vector128<double>(CommandPosition obj) => obj.internalVec;

    public static implicit operator CommandPosition(PointF obj) => new(obj.X, obj.Y);
    public static implicit operator PointF(CommandPosition obj) => new(obj.X, obj.Y);

    public static implicit operator CommandPosition(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandPosition obj) => new(obj.X, obj.Y);
}