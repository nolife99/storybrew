namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using osuTK;
using Vector2 = System.Numerics.Vector2;

///<summary> Base structure for scale commands. </summary>
public readonly struct CommandScale : CommandValue, IEquatable<CommandScale>,
    IAdditionOperators<CommandScale, CommandScale, CommandScale>, ISubtractionOperators<CommandScale, CommandScale, CommandScale>,
    IMultiplyOperators<CommandScale, CommandScale, CommandScale>, IDivisionOperators<CommandScale, CommandScale, CommandScale>
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandScale(Vector2 vector) => internalVec = vector;

    /// <inheritdoc/>
    public bool Equals(CommandScale other) => internalVec == other.internalVec;

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is CommandScale scale && Equals(scale);

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

    public static CommandScale operator *(CommandScale left, CommandScale right) => left.internalVec * right.internalVec;

    public static CommandScale operator *(CommandScale left, CommandDecimal right)
        => new(left.internalVec.X * right, left.internalVec.Y * right);

    public static CommandScale operator /(CommandScale left, CommandScale right) => left.internalVec / right.internalVec;

    public static CommandScale operator /(CommandScale left, CommandDecimal right)
        => new(left.internalVec.X / right, left.internalVec.Y / right);

    public static bool operator ==(CommandScale left, CommandScale right) => left.Equals(right);
    public static bool operator !=(CommandScale left, CommandScale right) => !left.Equals(right);

    public static implicit operator CommandScale(osuTK.Vector2 vector) => Unsafe.As<osuTK.Vector2, CommandScale>(ref vector);

    public static implicit operator CommandScale(Vector2d vector) => new(vector.X, vector.Y);
    public static implicit operator CommandScale(SizeF vector) => vector.ToVector2();

    public static implicit operator CommandScale(CommandPosition position)
        => Unsafe.As<CommandPosition, CommandScale>(ref position);

    public static implicit operator osuTK.Vector2(CommandScale obj) => Unsafe.As<CommandScale, osuTK.Vector2>(ref obj);
    public static implicit operator Vector2d(CommandScale obj) => new(obj.X, obj.Y);
    public static implicit operator SizeF(CommandScale vector) => new(vector.internalVec);

    public static implicit operator CommandPosition(CommandScale position)
        => Unsafe.As<CommandScale, CommandPosition>(ref position);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2(CommandScale obj) => obj.internalVec;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CommandScale(Vector2 vector) => Unsafe.As<Vector2, CommandScale>(ref vector);
}