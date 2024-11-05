using System;
using System.Drawing;
using System.IO;
using osuTK;
using osuTK.Graphics;

namespace StorybrewCommon.Storyboarding.CommandValues;

///<summary> Base struct for coloring commands. </summary>
public readonly struct CommandColor : CommandValue, IEquatable<CommandColor>
{
    ///<summary> Represents a <see cref="CommandColor"/> value as the color black. </summary>
    public static readonly CommandColor Black = new(0, 0, 0);

    ///<summary> Represents a <see cref="CommandColor"/> value as the color white. </summary>
    public static readonly CommandColor White = new(1, 1, 1);

    ///<summary> Represents a <see cref="CommandColor"/> value as the color red. </summary>
    public static readonly CommandColor Red = new(1, 0, 0);

    ///<summary> Represents a <see cref="CommandColor"/> value as the color green. </summary>
    public static readonly CommandColor Green = new(0, 1, 0);

    ///<summary> Represents a <see cref="CommandColor"/> value as the color blue. </summary>
    public static readonly CommandColor Blue = new(0, 0, 1);

    readonly double r, g, b;

    ///<summary> Gets the red value of this instance. </summary>
    public byte R => toByte(r);

    ///<summary> Gets the green value of this instance. </summary>
    public byte G => toByte(g);

    ///<summary> Gets the blue value of this instance. </summary>
    public byte B => toByte(b);

    ///<summary> Constructs a new <see cref="CommandColor"/> from red, green, and blue values from 0.0 to 1.0. </summary>
    public CommandColor(double r = 1, double g = 1, double b = 1)
    {
        if (double.IsNaN(r) || double.IsInfinity(r) ||
            double.IsNaN(g) || double.IsInfinity(g) ||
            double.IsNaN(b) || double.IsInfinity(b))
            throw new InvalidDataException($"Invalid command color {r},{g},{b}");

        this.r = r;
        this.g = g;
        this.b = b;
    }

    ///<summary> Returns whether or not this instance and <paramref name="other"/> are equal to each other. </summary>
    public bool Equals(CommandColor other) => r == other.r && g == other.g && b == other.b;

    ///<summary> Returns whether or not this instance and <paramref name="obj"/> are equal to each other. </summary>
    public override bool Equals(object obj) => obj is not null && obj is CommandColor color && Equals(color);

    ///<summary> Returns a 32-bit integer hash that represents this instance's color information, with 8 bits per channel. </summary>
    ///<remarks> Some color information could be lost. </remarks>
    public override int GetHashCode() => 0 | (B << 16) | (G << 8) | R;

    ///<summary> Converts this instance into a string, formatted as "R, G, B". </summary>
    public override string ToString() => $"{R}, {G}, {B}";

    ///<summary> Converts this instance into a .osb formatted string, formatted as "R,G,B". </summary>
    public string ToOsbString(ExportSettings exportSettings) => $"{R},{G},{B}";

    ///<summary> Returns a <see cref="CommandColor"/> structure that represents the hash code's color information. </summary>
    ///<remarks> Some color information could be lost. </remarks>
    public static CommandColor FromHashCode(int code) => FromRgb(code & 0xFF, (code >> 8) & 0xFF, (code >> 16) & 0xFF);

    ///<summary> Creates a <see cref="CommandColor"/> from RGB byte values. </summary>
    public static CommandColor FromRgb(int r, int g, int b) => new(r / 255d, g / 255d, b / 255d);

    ///<summary> Creates a <see cref="CommandColor"/> from HSB values. <para>Hue: 0 - 360 | Saturation: 0 - 1 | Brightness: 0 - 1</para></summary>
    public static CommandColor FromHsb(double hue, double saturation, double brightness)
    {
        var hi = (int)(hue / 60) % 6;
        var f = hue / 60 - (int)(hue / 60);

        var v = brightness;
        var p = v * (1 - saturation);
        var q = v * (1 - f * saturation);
        var t = v * (1 - (1 - f) * saturation);

        return hi switch
        {
            0 => new(v, t, p),
            1 => new(q, v, p),
            2 => new(p, v, t),
            3 => new(p, q, v),
            4 => new(t, p, v),
            _ => new(v, p, q)
        };
    }

    ///<summary> Creates a <see cref="CommandColor"/> from a hex-code color. </summary>
    public static CommandColor FromHtml(string htmlColor) => ColorTranslator.FromHtml(htmlColor.StartsWith('#') ? htmlColor : "#" + htmlColor);

    static byte toByte(double x) => byte.CreateTruncating(x * 255);

#pragma warning disable CS1591
    public static implicit operator Color4(CommandColor obj) => new(obj.R, obj.G, obj.B, 255);
    public static implicit operator CommandColor(Color4 obj) => new(obj.R, obj.G, obj.B);
    public static implicit operator CommandColor(Color obj) => new(obj.R / 255d, obj.G / 255d, obj.B / 255d);
    public static implicit operator Color(CommandColor obj) => Color.FromArgb(obj.R, obj.G, obj.B);
    public static implicit operator CommandColor(string hexCode) => FromHtml(hexCode);
    public static bool operator ==(CommandColor left, CommandColor right) => left.Equals(right);
    public static bool operator !=(CommandColor left, CommandColor right) => !left.Equals(right);
    public static CommandColor operator +(CommandColor left, CommandColor right) => new(left.r + right.r, left.g + right.g, left.b + right.b);
    public static CommandColor operator -(CommandColor left, CommandColor right) => new(left.r - right.r, left.g - right.g, left.b - right.b);
    public static CommandColor operator *(CommandColor left, CommandColor right) => new(left.r * right.r, left.g * right.g, left.b * right.b);
    public static CommandColor operator *(CommandColor left, double right) => new(left.r * right, left.g * right, left.b * right);
    public static CommandColor operator *(double left, CommandColor right) => right * left;
    public static CommandColor operator /(CommandColor left, double right) => new(left.r / right, left.g / right, left.b / right);
}