namespace StorybrewCommon.Subtitles;

using System;
using System.Drawing;
using System.Numerics;
using osuTK.Graphics;
using Storyboarding.CommandValues;

///<summary> Base struct for coloring commands. </summary>
public readonly struct FontColor : IEquatable<FontColor>
{
    /// <summary> Represents a <see cref="FontColor" /> value as the color black. </summary>
    public static readonly FontColor Black = new(0, 0, 0);

    /// <summary> Represents a <see cref="FontColor" /> value as the color white. </summary>
    public static readonly FontColor White = new(1);

    /// <summary> Represents a <see cref="FontColor" /> value as the color red. </summary>
    public static readonly FontColor Red = new(1, 0, 0);

    /// <summary> Represents a <see cref="FontColor" /> value as the color green. </summary>
    public static readonly FontColor Green = new(0, 1, 0);

    /// <summary> Represents a <see cref="FontColor" /> value as the color blue. </summary>
    public static readonly FontColor Blue = new(0, 0);

    readonly double r, g, b, a;

    ///<summary> Gets the red value of this instance. </summary>
    public byte R => byte.CreateTruncating(r * 255);

    ///<summary> Gets the green value of this instance. </summary>
    public byte G => byte.CreateTruncating(g * 255);

    ///<summary> Gets the blue value of this instance. </summary>
    public byte B => byte.CreateTruncating(b * 255);

    ///<summary> Gets the blue value of this instance. </summary>
    public byte A => byte.CreateTruncating(a * 255);

    /// <summary> Constructs a new <see cref="CommandColor" /> from red, green, and blue values from 0.0 to 1.0. </summary>
    public FontColor(double r = 1, double g = 1, double b = 1, double a = 1)
    {
        if (double.IsNaN(r) || double.IsInfinity(r) || double.IsNaN(g) || double.IsInfinity(g) || double.IsNaN(b) ||
            double.IsInfinity(b) || double.IsNaN(a) || double.IsInfinity(a) || r > 1 || g > 1 || b > 1 || a > 1 ||
            r < 0 || g < 0 || b < 0 || a < 0)
            throw new ArgumentException($"Invalid font color {r}, {g}, {b}");

        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }

    /// <summary> Returns whether this instance and <paramref name="other" /> are equal to each other. </summary>
    public bool Equals(FontColor other) => r == other.r && g == other.g && b == other.b && a == other.a;

    /// <summary> Returns whether this instance and <paramref name="obj" /> are equal to each other. </summary>
    public override bool Equals(object obj) => obj is FontColor color && Equals(color);

    /// <summary> Returns a 32-bit integer hash that represents this instance's color information, with 8 bits per channel. </summary>
    /// <remarks> Some color information could be lost. </remarks>
    public override int GetHashCode() => ((Color)this).ToArgb();

    ///<summary> Converts this instance into a string, formatted as "R, G, B, A". </summary>
    public override string ToString() => $"{R}, {G}, {B}, {A}";

    /// <summary> Returns a <see cref="FontColor" /> structure that represents the hash code's color information. </summary>
    /// <remarks> Some color information could be lost. </remarks>
    public static FontColor FromHashCode(int code) => Color.FromArgb(code);

    /// <summary> Creates a <see cref="FontColor" /> from RGB byte values. </summary>
    public static FontColor FromRgba(int r, int g, int b, int a) => new(r / 255d, g / 255d, b / 255d, a / 255d);

    /// <summary>
    ///     Creates a <see cref="Vector4" /> containing the hue, saturation, brightness, and alpha values from a
    ///     <see cref="FontColor" />.
    /// </summary>
    public static Vector4 ToHsb(FontColor rgb)
    {
        var max = Math.Max(rgb.r, Math.Max(rgb.g, rgb.b));
        var min = Math.Min(rgb.r, Math.Min(rgb.g, rgb.b));
        var delta = max - min;

        var hue = 0d;
        if (rgb.r == max)
            hue = (rgb.g - rgb.b) / delta;
        else if (rgb.g == max)
            hue = 2 + (rgb.b - rgb.r) / delta;
        else if (rgb.b == max) hue = 4 + (rgb.r - rgb.g) / delta;
        hue /= 6;
        if (hue < 0) ++hue;

        var saturation = double.IsNegative(max) ? 0 : 1 - min / max;

        return new((float)hue, (float)saturation, (float)max, (float)rgb.a);
    }

    /// <summary>
    ///     Creates a <see cref="Vector4" /> containing the hue, saturation, brightness, and alpha values from a
    ///     <see cref="FontColor" />.
    /// </summary>
    public static FontColor FromHsb(Vector4 hsba)
    {
        double hue = hsba.X * 360, saturation = hsba.Y, brightness = hsba.Z;
        var c = brightness * saturation;

        var h = hue / 60;
        var x = c * (1 - Math.Abs(h % 2 - 1));

        double r, g, b;
        switch (h)
        {
            case >= 0 and < 1:
                r = c;
                g = x;
                b = 0;
                break;
            case >= 1 and < 2:
                r = x;
                g = c;
                b = 0;
                break;
            case >= 2 and < 3:
                r = 0;
                g = c;
                b = x;
                break;
            case >= 3 and < 4:
                r = 0;
                g = x;
                b = c;
                break;
            case >= 4 and < 5:
                r = x;
                g = 0;
                b = c;
                break;
            case >= 5 and < 6:
                r = c;
                g = 0;
                b = x;
                break;
            default:
                r = 0;
                g = 0;
                b = 0;
                break;
        }

        var m = brightness - c;
        return new(r + m, g + m, b + m, hsba.W);
    }

    /// <summary> Creates a <see cref="FontColor" /> from a hex-code color. </summary>
    public static FontColor FromHtml(string htmlColor)
        => ColorTranslator.FromHtml(htmlColor.StartsWith('#') ? htmlColor : "#" + htmlColor);

#pragma warning disable CS1591
    public static implicit operator FontColor(CommandColor obj) => new(obj.R / 255d, obj.G / 255d, obj.B / 255d);
    public static implicit operator CommandColor(FontColor obj) => new(obj.r, obj.g, obj.b);
    public static implicit operator FontColor(Color obj) => new(obj.R / 255d, obj.G / 255d, obj.B / 255d, obj.A / 255d);
    public static implicit operator Color(FontColor obj) => Color.FromArgb(obj.A, obj.R, obj.G, obj.B);
    public static implicit operator FontColor(Color4 obj) => new(obj.R, obj.G, obj.B, obj.A);
    public static implicit operator Color4(FontColor obj) => new(obj.R, obj.G, obj.B, obj.A);
    public static implicit operator FontColor(string hexCode) => FromHtml(hexCode);
    public static implicit operator FontColor(int channel) => FromHashCode(channel);
    public static bool operator ==(FontColor left, FontColor right) => left.Equals(right);
    public static bool operator !=(FontColor left, FontColor right) => !left.Equals(right);

    public static FontColor operator +(FontColor left, FontColor right)
        => new(left.r + right.r, left.g + right.g, left.b + right.b, left.a + right.a);

    public static FontColor operator -(FontColor left, FontColor right)
        => new(left.r - right.r, left.g - right.g, left.b - right.b, left.a - right.a);

    public static FontColor operator *(FontColor left, FontColor right)
        => new(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);

    public static FontColor operator *(FontColor left, double right)
        => new(left.r * right, left.g * right, left.b * right, left.a * right);

    public static FontColor operator *(double left, FontColor right) => right * left;

    public static FontColor operator /(FontColor left, double right)
        => new(left.r / right, left.g / right, left.b / right, left.a / right);
}