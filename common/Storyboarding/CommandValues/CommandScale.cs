namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Util;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Temporary;

///<summary> Base structure for scale commands. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandScale
    : ICommandValue, IAdditionOperators<CommandScale, CommandScale, CommandScale>,
        ISubtractionOperators<CommandScale, CommandScale, CommandScale>,
        IMultiplyOperators<CommandScale, CommandScale, CommandScale>,
        IDivisionOperators<CommandScale, CommandScale, CommandScale>, IUnaryNegationOperators<CommandScale, CommandScale>
{
    internal readonly Vector2 internalVec;

    ///<summary> Represents a scale vector in which all values are 1 (one). </summary>
    public static readonly CommandScale One = new(1, 1);

    ///<summary> Gets the X value of this instance. </summary>
    public CommandDecimal X => internalVec.X;

    ///<summary> Gets the Y value of this instance. </summary>
    public CommandDecimal Y => internalVec.Y;

    /// <summary> Constructs a <see cref="CommandScale"/> from an X and Y value. </summary>
    public CommandScale(CommandDecimal x, CommandDecimal y) => internalVec = new(x, y);

    /// <summary> Constructs a <see cref="CommandScale"/> from a value. </summary>
    public CommandScale(CommandDecimal value) : this(value, value) { }

    /// <summary> Constructs a <see cref="CommandScale"/> from a <see cref="Vector2"/>. </summary>
    public CommandScale(Vector2 vector) => internalVec = vector;

    ///<summary> Converts this instance to a .osb string. </summary>
    TempList<char> ICommandValue.ToOsbString(ExportSettings exportSettings) => StringHelper.Interpolate(
        exportSettings.NumberFormat,
        $"{(exportSettings.UseFloatForMove ? X : (int)float.Round(X))},{(exportSettings.UseFloatForMove ? Y : (int)float.Round(Y))}");

#pragma warning disable CS1591
    public static CommandScale operator +(CommandScale left, CommandScale right) => left.internalVec + right.internalVec;
    public static CommandScale operator -(CommandScale left, CommandScale right) => left.internalVec - right.internalVec;
    public static CommandScale operator -(CommandScale value) => -value.internalVec;
    public static CommandScale operator *(CommandScale left, CommandScale right) => left.internalVec * right.internalVec;
    public static CommandScale operator *(CommandScale left, CommandDecimal right) => left.internalVec * right;
    public static CommandScale operator /(CommandScale left, CommandScale right) => left.internalVec / right.internalVec;
    public static CommandScale operator /(CommandScale left, CommandDecimal right) => left.internalVec / right;

    public static implicit operator CommandScale(OpenTK.Mathematics.Vector2 obj) => new(obj.X, obj.Y);

    public static implicit operator OpenTK.Mathematics.Vector2(CommandScale obj) => new(obj.X, obj.Y);

    public static implicit operator CommandScale(SizeF obj) => new(obj.Width, obj.Height);
    public static implicit operator SizeF(CommandScale obj) => new(obj.X, obj.Y);

    public static implicit operator CommandScale(CommandPosition obj) => obj.internalVec;
    public static implicit operator CommandPosition(CommandScale obj) => obj.internalVec;

    public static implicit operator CommandScale(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandScale obj) => obj.internalVec;
}