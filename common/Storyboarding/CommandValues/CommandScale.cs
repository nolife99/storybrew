namespace StorybrewCommon.Storyboarding.CommandValues;

using System.Numerics;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using Vector2 = System.Numerics.Vector2;

///<summary> Base structure for scale commands. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandScale
    : CommandValue, IAdditionOperators<CommandScale, CommandScale, CommandScale>,
        ISubtractionOperators<CommandScale, CommandScale, CommandScale>,
        IMultiplyOperators<CommandScale, CommandScale, CommandScale>,
        IDivisionOperators<CommandScale, CommandScale, CommandScale>, IUnaryNegationOperators<CommandScale, CommandScale>
{
    readonly Vector2 internalVec;

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

    /// <inheritdoc/>
    public bool Equals(CommandScale other) => internalVec == other.internalVec;

    /// <inheritdoc/>
    public override int GetHashCode() => internalVec.GetHashCode();

    ///<summary> Converts this instance to a .osb string. </summary>
    public string ToOsbString(ExportSettings exportSettings)
        => $"{X.ToOsbString(exportSettings)},{Y.ToOsbString(exportSettings)}";

    ///<summary> Converts this instance to a string. </summary>
    public override string ToString() => ToOsbString(ExportSettings.Default);

#pragma warning disable CS1591
    public static CommandScale operator +(CommandScale left, CommandScale right) => left.internalVec + right.internalVec;
    public static CommandScale operator -(CommandScale left, CommandScale right) => left.internalVec - right.internalVec;
    public static CommandScale operator -(CommandScale value) => -value.internalVec;
    public static CommandScale operator *(CommandScale left, CommandScale right) => left.internalVec * right.internalVec;
    public static CommandScale operator *(CommandScale left, CommandDecimal right) => left.internalVec * right;
    public static CommandScale operator /(CommandScale left, CommandScale right) => left.internalVec / right.internalVec;
    public static CommandScale operator /(CommandScale left, CommandDecimal right) => left.internalVec / right;

    public static implicit operator CommandScale(OpenTK.Mathematics.Vector2 obj) => new(obj.X, obj.Y);

    public static implicit operator OpenTK.Mathematics.Vector2(CommandScale obj)
        => new(obj.internalVec.X, obj.internalVec.Y);

    public static implicit operator CommandScale(Vector2d obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2d(CommandScale obj) => new(obj.internalVec.X, obj.internalVec.Y);

    public static implicit operator CommandScale(SizeF obj) => new(obj.Width, obj.Height);
    public static implicit operator SizeF(CommandScale obj) => new(obj.internalVec.X, obj.internalVec.Y);

    public static implicit operator CommandScale(CommandPosition obj) => new(obj.X, obj.Y);
    public static implicit operator CommandPosition(CommandScale obj) => new(obj.internalVec.X, obj.internalVec.Y);

    public static implicit operator CommandScale(Vector2 obj) => new(obj.X, obj.Y);
    public static implicit operator Vector2(CommandScale obj) => new(obj.internalVec.X, obj.internalVec.Y);
}