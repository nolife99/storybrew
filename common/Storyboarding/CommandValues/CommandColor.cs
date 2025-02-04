namespace StorybrewCommon.Storyboarding.CommandValues;

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

///<summary> Base struct for coloring commands. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandColor : CommandValue
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

    ///<summary> Gets the red value of this instance. </summary>
    public byte R => toByte(internalVec.X);

    ///<summary> Gets the green value of this instance. </summary>
    public byte G => toByte(internalVec.Y);

    ///<summary> Gets the blue value of this instance. </summary>
    public byte B => toByte(internalVec.Z);

    /// <summary> Constructs a new <see cref="CommandColor"/> from red, green, and blue values from 0.0 to 1.0. </summary>
    public CommandColor(double r = 1, double g = 1, double b = 1)
    {
        if (double.IsNaN(r) ||
            double.IsInfinity(r) ||
            double.IsNaN(g) ||
            double.IsInfinity(g) ||
            double.IsNaN(b) ||
            double.IsInfinity(b)) throw new InvalidDataException($"Invalid command color {r},{g},{b}");

        internalVec = new((float)r, (float)g, (float)b);
    }

    /// <summary> Returns whether this instance and <paramref name="other"/> are equal to each other. </summary>
    public bool Equals(CommandColor other) => internalVec == other.internalVec;

    /// <summary> Returns a 32-bit integer hash that represents this instance's color information, with 8 bits per channel. </summary>
    /// <remarks> Some color information could be lost. </remarks>
    public override int GetHashCode() => 0 | B << 16 | G << 8 | R;

    ///<summary> Converts this instance into a string, formatted as "R, G, B". </summary>
    public override string ToString() => $"{R}, {G}, {B}";

    ///<summary> Converts this instance into a .osb formatted string, formatted as "R,G,B". </summary>
    public string ToOsbString(ExportSettings exportSettings) => $"{R},{G},{B}";

    /// <summary> Creates a <see cref="CommandColor"/> from RGB byte values. </summary>
    public static CommandColor FromRgb(int r, int g, int b) => new Vector3(r / 255f, g / 255f, b / 255f);

    /// <summary>Creates a <see cref="CommandColor"/> from HSB values.
    ///     <para>Hue: 0 - 360 | Saturation: 0 - 1 | Brightness: 0 - 1</para>
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

    /// <summary> Creates a <see cref="CommandColor"/> from a hex-code color. </summary>
    public static CommandColor FromHtml(string htmlColor)
        => Color.ParseHex(htmlColor.StartsWith('#') ? htmlColor : '#' + htmlColor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static byte toByte(float x) => byte.CreateSaturating(x * 255);

#pragma warning disable CS1591
    public static implicit operator Color4(CommandColor obj)
        => new(obj.internalVec.X, obj.internalVec.Y, obj.internalVec.Z, 1);

    public static implicit operator CommandColor(Color4 obj) => Unsafe.As<Color4, CommandColor>(ref obj);
    public static implicit operator Rgba32(CommandColor obj) => new(obj.internalVec);
    public static implicit operator CommandColor(Rgba32 obj) => new Vector3(obj.R, obj.G, obj.B) / 255;
    public static implicit operator Color(CommandColor obj) => new Rgba64(new Vector4(obj.internalVec, 1));

    public static implicit operator CommandColor(Color obj)
    {
        var rgba = (Vector4)obj;
        return Unsafe.As<Vector4, CommandColor>(ref rgba);
    }

    public static implicit operator CommandColor(string hexCode) => FromHtml(hexCode);
    public static implicit operator Vector3(CommandColor obj) => Unsafe.As<CommandColor, Vector3>(ref obj);
    public static implicit operator CommandColor(Vector3 obj) => Unsafe.As<Vector3, CommandColor>(ref obj);

    public static CommandColor operator +(CommandColor left, CommandColor right) => left.internalVec + right.internalVec;
    public static CommandColor operator -(CommandColor left, CommandColor right) => left.internalVec - right.internalVec;
    public static CommandColor operator *(CommandColor left, CommandColor right) => left.internalVec * right.internalVec;

    public static CommandColor operator *(CommandColor left, CommandDecimal right) => left.internalVec * right;
    public static CommandColor operator *(CommandDecimal left, CommandColor right) => right.internalVec * left;
    public static CommandColor operator /(CommandColor left, CommandDecimal right) => left.internalVec / right;
}