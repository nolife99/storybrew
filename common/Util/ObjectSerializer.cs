namespace StorybrewCommon.Util;

using System;
using System.Collections.Frozen;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Storyboarding.CommandValues;
using Color4 = OpenTK.Mathematics.Color4;

#pragma warning disable CS1591
public abstract class ObjectSerializer
{
    static readonly FrozenSet<ObjectSerializer> serializers =
    [
        new SimpleObjectSerializer<int>(
            r => r.ReadInt32(),
            (w, v) => w.Write(Unsafe.Unbox<int>(v)),
            v => int.Parse(v, CultureInfo.InvariantCulture),
            v => Unsafe.Unbox<int>(v).ToString(CultureInfo.InvariantCulture)),
        new SimpleObjectSerializer<float>(
            r => r.ReadSingle(),
            (w, v) => w.Write(Unsafe.Unbox<float>(v)),
            v => float.Parse(v, CultureInfo.InvariantCulture),
            v => Unsafe.Unbox<float>(v).ToString(CultureInfo.InvariantCulture)),
        new SimpleObjectSerializer<double>(
            r => r.ReadDouble(),
            (w, v) => w.Write(Unsafe.Unbox<double>(v)),
            v => double.Parse(v, CultureInfo.InvariantCulture),
            v => Unsafe.Unbox<double>(v).ToString(CultureInfo.InvariantCulture)),
        new SimpleObjectSerializer<string>(r => r.ReadString(), (w, v) => w.Write(Unsafe.As<string>(v))),
        new SimpleObjectSerializer<bool>(
            r => r.ReadBoolean(),
            (w, v) => w.Write(Unsafe.Unbox<bool>(v)),
            v => bool.Parse(v),
            v => Unsafe.Unbox<bool>(v).ToString()),
        new SimpleObjectSerializer<CommandScale>(
            r => new CommandScale(r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var vector = Unsafe.Unbox<CommandScale>(v);
                w.Write(vector.X);
                w.Write(vector.Y);
            },
            v =>
            {
                var split = v.Split(',');
                return new CommandScale(
                    double.Parse(split[0], CultureInfo.InvariantCulture),
                    double.Parse(split[1], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var vector = Unsafe.Unbox<CommandScale>(v);
                return vector.X + "," + vector.Y;
            }),
        new SimpleObjectSerializer<CommandPosition>(
            r => new CommandPosition(r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var vector = Unsafe.Unbox<CommandPosition>(v);
                w.Write(vector.X);
                w.Write(vector.Y);
            },
            v =>
            {
                var split = v.Split(',');
                return new CommandPosition(
                    double.Parse(split[0], CultureInfo.InvariantCulture),
                    double.Parse(split[1], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var vector = Unsafe.Unbox<CommandPosition>(v);
                return vector.X + "," + vector.Y;
            }),
        new SimpleObjectSerializer<Vector2>(
            r => new Vector2(r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var vector = Unsafe.Unbox<Vector2>(v);
                w.Write(vector.X);
                w.Write(vector.Y);
            },
            v =>
            {
                var split = v.Split(',');
                return new Vector2(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var vector = Unsafe.Unbox<Vector2>(v);
                return vector.X.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    vector.Y.ToString(CultureInfo.InvariantCulture);
            }),
        new SimpleObjectSerializer<Vector3>(
            r => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var vector = Unsafe.Unbox<Vector3>(v);
                w.Write(vector.X);
                w.Write(vector.Y);
                w.Write(vector.Z);
            },
            v =>
            {
                var split = v.Split(',');
                return new Vector3(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture),
                    float.Parse(split[2], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var vector = Unsafe.Unbox<Vector3>(v);
                return vector.X.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    vector.Y.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    vector.Z.ToString(CultureInfo.InvariantCulture);
            }),
        new SimpleObjectSerializer<OpenTK.Mathematics.Vector2>(
            r => new OpenTK.Mathematics.Vector2(r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var vector = Unsafe.Unbox<OpenTK.Mathematics.Vector2>(v);
                w.Write(vector.X);
                w.Write(vector.Y);
            },
            v =>
            {
                var split = v.Split(',');
                return new OpenTK.Mathematics.Vector2(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var vector = Unsafe.Unbox<OpenTK.Mathematics.Vector2>(v);
                return vector.X.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    vector.Y.ToString(CultureInfo.InvariantCulture);
            }),
        new SimpleObjectSerializer<OpenTK.Mathematics.Vector3>(
            r => new OpenTK.Mathematics.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var vector = Unsafe.Unbox<OpenTK.Mathematics.Vector3>(v);
                w.Write(vector.X);
                w.Write(vector.Y);
                w.Write(vector.Z);
            },
            v =>
            {
                var split = v.Split(',');
                return new OpenTK.Mathematics.Vector3(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture),
                    float.Parse(split[2], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var vector = Unsafe.Unbox<OpenTK.Mathematics.Vector3>(v);
                return vector.X.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    vector.Y.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    vector.Z.ToString(CultureInfo.InvariantCulture);
            }),
        new SimpleObjectSerializer<Color4>(
            r => new Color4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
            (w, v) =>
            {
                var color = Unsafe.Unbox<Color4>(v);
                w.Write(color.R);
                w.Write(color.G);
                w.Write(color.B);
                w.Write(color.A);
            },
            v =>
            {
                var split = v.Split(',');
                return new Color4(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture),
                    float.Parse(split[2], CultureInfo.InvariantCulture),
                    float.Parse(split[3], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var color = Unsafe.Unbox<Color4>(v);
                return color.R.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.G.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.B.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.A.ToString(CultureInfo.InvariantCulture);
            }),
        new SimpleObjectSerializer<Rgba32>(
            r => new Rgba32(r.ReadUInt32()),
            (w, v) => w.Write(((Rgba32)v).PackedValue),
            v =>
            {
                var split = v.Split(',');
                return new Rgba32(
                    byte.Parse(split[0], CultureInfo.InvariantCulture),
                    byte.Parse(split[1], CultureInfo.InvariantCulture),
                    byte.Parse(split[2], CultureInfo.InvariantCulture),
                    byte.Parse(split[3], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var color = Unsafe.Unbox<Rgba32>(v);
                return color.R.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.G.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.B.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.A.ToString(CultureInfo.InvariantCulture);
            }),
        new SimpleObjectSerializer<Color>(
            r => (Color)new Rgba32(r.ReadUInt32()),
            (w, v) => w.Write(((Rgba32)v).PackedValue),
            v =>
            {
                var split = v.Split(',');
                return Color.FromRgba(
                    byte.Parse(split[0], CultureInfo.InvariantCulture),
                    byte.Parse(split[1], CultureInfo.InvariantCulture),
                    byte.Parse(split[2], CultureInfo.InvariantCulture),
                    byte.Parse(split[3], CultureInfo.InvariantCulture));
            },
            v =>
            {
                var color = Unsafe.Unbox<Color>(v).ToPixel<Rgba32>();
                return color.R.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.G.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.B.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    color.A.ToString(CultureInfo.InvariantCulture);
            })
    ];

    protected abstract bool CanSerialize(string typeName);
    protected abstract void WriteValue(BinaryWriter writer, object value);
    protected abstract object ReadValue(BinaryReader reader);
    protected abstract string ToString(object value);
    protected abstract object FromString(string value);

    public static object Read(BinaryReader reader)
    {
        var typeName = reader.ReadString();
        if (string.IsNullOrEmpty(typeName)) return null;

        var serializer = GetSerializer(typeName) ??
            throw new NotSupportedException($"Cannot read objects of type {typeName}");

        return serializer.ReadValue(reader);
    }

    public static void Write(BinaryWriter writer, object value)
    {
        if (value is null)
        {
            writer.Write("");
            return;
        }

        var typeName = value.GetType().FullName;

        var serializer = GetSerializer(typeName) ??
            throw new NotSupportedException($"Cannot write objects of type {typeName}");

        writer.Write(typeName);
        serializer.WriteValue(writer, value);
    }

    public static object FromString(string typeName, string value)
    {
        if (typeName == "") return null;

        var serializer = GetSerializer(typeName) ??
            throw new NotSupportedException($"Cannot read objects of type {typeName}");

        return serializer.FromString(value);
    }

    public static string ToString(Type type, object value)
    {
        if (value is null) return "";

        var typeName = type.FullName;

        var serializer = GetSerializer(typeName) ??
            throw new NotSupportedException($"Cannot write objects of type {typeName}");

        return serializer.ToString(value);
    }

    static ObjectSerializer GetSerializer(string typeName)
        => serializers.FirstOrDefault(serializer => serializer.CanSerialize(typeName));
}

public class SimpleObjectSerializer<T>(Func<BinaryReader, object> read,
    Action<BinaryWriter, object> write,
    Func<string, object> fromString = null,
    Func<object, string> toString = null) : ObjectSerializer
{
    protected override bool CanSerialize(string typeName) => typeName == typeof(T).FullName;
    protected override object ReadValue(BinaryReader reader) => read(reader);
    protected override void WriteValue(BinaryWriter writer, object value) => write(writer, value);
    protected override object FromString(string value) => fromString?.Invoke(value) ?? value;
    protected override string ToString(object value) => toString?.Invoke(value) ?? Unsafe.As<string>(value);
}