namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic.Temporary;

///<summary> Base struct for coloring commands. </summary>
public readonly record struct CommandColor : ICommandValue<CommandColor>,
    IMultiplyOperators<CommandColor, CommandColor, CommandColor>,
    IDivisionOperators<CommandColor, CommandColor, CommandColor>
{
    /// <summary> Represents a <see cref="CommandColor"/> value as the color black. </summary>
    public static readonly CommandColor Black = new(0, 0, 0);

    /// <summary> Represents a <see cref="CommandColor"/> value as the color white. </summary>
    public static readonly CommandColor White = new(1);

    /// <summary> Represents a <see cref="CommandColor"/> value as the color red. </summary>
    public static readonly CommandColor Red = new(1, 0, 0);

    /// <summary> Represents a <see cref="CommandColor"/> value as the color green. </summary>
    public static readonly CommandColor Green = new(0, 1, 0);

    /// <summary> Represents a <see cref="CommandColor"/> value as the color blue. </summary>
    public static readonly CommandColor Blue = new(0, 0);

    readonly Vector3 internalVec;

    /// <summary> Constructs a new <see cref="CommandColor"/> from red, green, and blue values from 0.0 to 1.0. </summary>
    public CommandColor(double r = 1, double g = 1, double b = 1)
    {
        if (!double.IsFinite(r) || !double.IsFinite(g) || !double.IsFinite(b))
            throw new InvalidDataException($"Invalid command color {r},{g},{b}");

        internalVec = new((float)r, (float)g, (float)b);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    CommandColor(Vector3 vec) => internalVec = vec;

    ///<summary> Gets the red value of this instance. </summary>
    public byte R
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => toByte(internalVec.X);
    }

    ///<summary> Gets the green value of this instance. </summary>
    public byte G
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => toByte(internalVec.Y);
    }

    ///<summary> Gets the blue value of this instance. </summary>
    public byte B
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => toByte(internalVec.Z);
    }

    TempList<char> ICommandValue<CommandColor>.ToOsbString(ExportSettings exportSettings)
    {
        Span<char> temp = stackalloc char[3];
        var list = TempList.Create<char>();

        R.TryFormat(temp, out var written, provider: exportSettings.NumberFormat);
        list.AddRange(temp[..written]);
        list.Add(',');

        G.TryFormat(temp, out written, provider: exportSettings.NumberFormat);
        list.AddRange(temp[..written]);
        list.Add(',');

        B.TryFormat(temp, out written, provider: exportSettings.NumberFormat);
        list.AddRange(temp[..written]);

        return list;
    }

    /// <summary> Creates a <see cref="CommandColor"/> from RGB byte values. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor FromRgb(int r, int g, int b) => new Vector3(r / 255f, g / 255f, b / 255f);

    /// <summary>
    /// Creates a <see cref="CommandColor"/> from HSB values.
    /// <para> Hue: 0 - 360 | Saturation: 0 - 1 | Brightness: 0 - 1 </para>
    /// </summary>
    public static CommandColor FromHsb(double hue, double saturation, double brightness)
    {
        var hi = (int)(hue / 60) % 6;
        var f = hue / 60 - (int)(hue / 60);

        var p = brightness * (1 - saturation);
        var q = brightness * (1 - f * saturation);
        var t = brightness * (1 - (1 - f) * saturation);

        return hi switch
        {
            0 => new(brightness, t, p),
            1 => new(q, brightness, p),
            2 => new(p, brightness, t),
            3 => new(p, q, brightness),
            4 => new(t, p, brightness),
            _ => new(brightness, p, q)
        };
    }

    /// <summary> Performs a linear interpolation between two vectors based on the given weighting. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor Lerp(CommandColor a, CommandColor b, float t)
        => new(Vector3.Lerp(a.internalVec, b.internalVec, t));

    /// <summary> Creates a <see cref="CommandColor"/> from a hex-code color. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor FromHtml(string htmlColor) => Color.ParseHex(htmlColor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static byte toByte(float x) => byte.CreateSaturating(x * 255);

#pragma warning disable CS1591

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Rgba32(CommandColor obj) => new(obj.internalVec);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CommandColor(Rgba32 obj) => obj.ToVector4().AsVector3();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Color(CommandColor obj) => Color.FromScaledVector(new(obj.internalVec, 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CommandColor(Color obj) => obj.ToScaledVector4().AsVector3();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CommandColor(string hexCode) => FromHtml(hexCode);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector3(CommandColor obj) => obj.internalVec;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CommandColor(Vector3 obj) => new(obj);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator +(CommandColor left, CommandColor right)
        => left.internalVec + right.internalVec;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator -(CommandColor left, CommandColor right)
        => left.internalVec - right.internalVec;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator *(CommandColor left, CommandColor right)
        => left.internalVec * right.internalVec;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator /(CommandColor left, CommandColor right)
        => left.internalVec / right.internalVec;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator *(CommandColor left, CommandDecimal right) => left.internalVec * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator *(CommandDecimal left, CommandColor right) => right.internalVec * left;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CommandColor operator /(CommandColor left, CommandDecimal right) => left.internalVec / right;
}