namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using BrewLib.Util;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;

///<summary> Base structure for scale commands. </summary>
public readonly record struct CommandScale : ICommandValue<CommandScale>,
    IMultiplyOperators<CommandScale, CommandScale, CommandScale>,
    IDivisionOperators<CommandScale, CommandScale, CommandScale>, IUnaryNegationOperators<CommandScale, CommandScale>
{
    ///<summary> Represents a scale vector in which all values are 1 (one). </summary>
    public static readonly CommandScale One = new(1, 1);

    readonly Vector128<double> internalVec;

    /// <summary> Constructs a <see cref="CommandScale"/> from an X and Y value. </summary>
    public CommandScale(CommandDecimal x, CommandDecimal y) => internalVec = Vector128.Create(x, y);

    /// <summary> Constructs a <see cref="CommandScale"/> from a value. </summary>
    public CommandScale(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandScale"/> from a <see cref="Vector2"/>. </summary>
    public CommandScale(Vector2 vector) => internalVec = Vector128.Create(vector.X, vector.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CommandScale(Vector128<double> vec) => internalVec = vec;

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.GetLower().ToScalar();

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.GetUpper().ToScalar();

    ///<summary> Converts this instance to a .osb string. </summary>
    TempList<char> ICommandValue<CommandScale>.ToOsbString(ExportSettings exportSettings)
        => StringHelper.Interpolate(exportSettings.NumberFormat,
            $"{(exportSettings.UseFloatForMove ? X : (int)float.Round(X))},{(exportSettings.UseFloatForMove ? Y : (int)float.Round(Y))}");

    /// <summary> Performs a linear interpolation between two vectors based on the given weighting. </summary>
    public static CommandScale Lerp(CommandScale a, CommandScale b, float t)
        => new(Vector128.Lerp(a.internalVec, b.internalVec, Vector128.Create((double)t)));

#pragma warning disable CS1591
    public static CommandScale operator +(CommandScale left, CommandScale right)
        => new(left.internalVec + right.internalVec);

    public static CommandScale operator -(CommandScale left, CommandScale right)
        => new(left.internalVec - right.internalVec);

    public static CommandScale operator -(CommandScale value) => new(-value.internalVec);

    public static CommandScale operator *(CommandScale left, CommandScale right)
        => new(left.internalVec * right.internalVec);

    public static CommandScale operator *(CommandScale left, CommandDecimal right) => new(left.internalVec * right);

    public static CommandScale operator /(CommandScale left, CommandScale right)
        => new(left.internalVec / right.internalVec);

    public static CommandScale operator /(CommandScale left, CommandDecimal right) => new(left.internalVec / right);

    public static implicit operator CommandScale(OpenTK.Mathematics.Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator OpenTK.Mathematics.Vector2(CommandScale obj) => new(obj.X, obj.Y);

    public static implicit operator CommandScale(SizeF obj) => new(obj.Width, obj.Height);
    public static implicit operator SizeF(CommandScale obj) => new(obj.X, obj.Y);

    public static implicit operator CommandScale(CommandPosition obj) => new(obj.internalVec);
    public static implicit operator CommandPosition(CommandScale obj) => new(obj.internalVec);

    public static implicit operator CommandScale(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandScale obj) => new(obj.X, obj.Y);
}