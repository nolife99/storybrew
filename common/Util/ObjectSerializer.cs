using System.Numerics;
using osuTK.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace StorybrewCommon.Util;

#pragma warning disable CS1591
public abstract class ObjectSerializer
{
    public abstract bool CanSerialize(string typeName);
    public abstract void WriteValue(BinaryWriter writer, object value);
    public abstract object ReadValue(BinaryReader reader);
    public abstract string ToString(object value);
    public abstract object FromString(string value);

    static readonly HashSet<ObjectSerializer> serializers =
    [
        new SimpleObjectSerializer<int>(r => r.ReadInt32(), (w, v) => w.Write((int)v), v => int.Parse(v, CultureInfo.InvariantCulture), v => ((int)v).ToString(CultureInfo.InvariantCulture)),
        new SimpleObjectSerializer<float>(r => r.ReadSingle(), (w, v) => w.Write((float)v), v => float.Parse(v, CultureInfo.InvariantCulture), v => ((float)v).ToString(CultureInfo.InvariantCulture)),
        new SimpleObjectSerializer<double>(r => r.ReadDouble(), (w, v) => w.Write((double)v), v => double.Parse(v, CultureInfo.InvariantCulture), v => ((double)v).ToString(CultureInfo.InvariantCulture)),
        new SimpleObjectSerializer<string>(r => r.ReadString(), (w, v) => w.Write((string)v)),
        new SimpleObjectSerializer<bool>(r => r.ReadBoolean(), (w, v) => w.Write((bool)v), v => bool.Parse(v), v => v.ToString()),
        new SimpleObjectSerializer<Vector2>(r =>
        {
            var x = r.ReadSingle();
            var y = r.ReadSingle();
            return new Vector2(x, y);
        }, (w, v) =>
        {
            var vector = (Vector2)v;
            w.Write(vector.X);
            w.Write(vector.Y);
        }, v =>
        {
            var split = v.Split(',');
            return new Vector2(float.Parse(split[0], CultureInfo.InvariantCulture), float.Parse(split[1], CultureInfo.InvariantCulture));
        }, v =>
        {
            var vector = (Vector2)v;
            return vector.X.ToString(CultureInfo.InvariantCulture) + "," + vector.Y.ToString(CultureInfo.InvariantCulture);
        }),
        new SimpleObjectSerializer<Vector3>(r =>
        {
            var x = r.ReadSingle();
            var y = r.ReadSingle();
            var z = r.ReadSingle();
            return new Vector3(x, y, z);
        }, (w, v) =>
        {
            var vector = (Vector3)v;
            w.Write(vector.X);
            w.Write(vector.Y);
            w.Write(vector.Z);
        }, v =>
        {
            var split = v.Split(',');
            return new Vector3(float.Parse(split[0], CultureInfo.InvariantCulture), float.Parse(split[1], CultureInfo.InvariantCulture), float.Parse(split[2], CultureInfo.InvariantCulture));
        }, v =>
        {
            var vector = (Vector3)v;
            return vector.X.ToString(CultureInfo.InvariantCulture) + "," + vector.Y.ToString(CultureInfo.InvariantCulture) + "," + vector.Z.ToString(CultureInfo.InvariantCulture);
        }),
        new SimpleObjectSerializer<osuTK.Vector2>(r =>
        {
            var x = r.ReadSingle();
            var y = r.ReadSingle();
            return new osuTK.Vector2(x, y);
        }, (w, v) =>
        {
            var vector = (osuTK.Vector2)v;
            w.Write(vector.X);
            w.Write(vector.Y);
        }, v =>
        {
            var split = v.Split(',');
            return new osuTK.Vector2(float.Parse(split[0], CultureInfo.InvariantCulture), float.Parse(split[1], CultureInfo.InvariantCulture));
        }, v =>
        {
            var vector = (osuTK.Vector2)v;
            return vector.X.ToString(CultureInfo.InvariantCulture) + "," + vector.Y.ToString(CultureInfo.InvariantCulture);
        }),
        new SimpleObjectSerializer<osuTK.Vector3>(r =>
        {
            var x = r.ReadSingle();
            var y = r.ReadSingle();
            var z = r.ReadSingle();
            return new osuTK.Vector3(x, y, z);
        }, (w, v) =>
        {
            var vector = (osuTK.Vector3)v;
            w.Write(vector.X);
            w.Write(vector.Y);
            w.Write(vector.Z);
        }, v =>
        {
            var split = v.Split(',');
            return new osuTK.Vector3(float.Parse(split[0], CultureInfo.InvariantCulture), float.Parse(split[1], CultureInfo.InvariantCulture), float.Parse(split[2], CultureInfo.InvariantCulture));
        }, v =>
        {
            var vector = (osuTK.Vector3)v;
            return vector.X.ToString(CultureInfo.InvariantCulture) + "," + vector.Y.ToString(CultureInfo.InvariantCulture) + "," + vector.Z.ToString(CultureInfo.InvariantCulture);
        }),
        new SimpleObjectSerializer<Color4>(r =>
        {
            var red = r.ReadByte();
            var green = r.ReadByte();
            var blue = r.ReadByte();
            var alpha = r.ReadByte();
            return new Color4(red, green, blue, alpha);
        }, (w, v) =>
        {
            var color = (Color4)v;
            w.Write((byte)(color.R * 255));
            w.Write((byte)(color.G * 255));
            w.Write((byte)(color.B * 255));
            w.Write((byte)(color.A * 255));
        }, v =>
        {
            var split = v.Split(',');
            return new Color4(float.Parse(split[0], CultureInfo.InvariantCulture), float.Parse(split[1], CultureInfo.InvariantCulture), float.Parse(split[2], CultureInfo.InvariantCulture), float.Parse(split[3], CultureInfo.InvariantCulture));
        }, v =>
        {
            var color = (Color4)v;
            return color.R.ToString(CultureInfo.InvariantCulture) + "," + color.G.ToString(CultureInfo.InvariantCulture) + "," + color.B.ToString(CultureInfo.InvariantCulture) + "," + color.A.ToString(CultureInfo.InvariantCulture);
        })
    ];

    public static object Read(BinaryReader reader)
    {
        var typeName = reader.ReadString();
        if (string.IsNullOrEmpty(typeName)) return null;
        
        var serializer = GetSerializer(typeName) ?? throw new NotSupportedException($"Cannot read objects of type {typeName}");
        return serializer.ReadValue(reader);
    }
    public static void Write(BinaryWriter writer, object value)
    {
        if (value is null)
        {
            writer.Write(string.Empty);
            return;
        }

        var typeName = value.GetType().FullName;

        var serializer = GetSerializer(typeName) ?? throw new NotSupportedException($"Cannot write objects of type {typeName}");
        writer.Write(typeName);
        serializer.WriteValue(writer, value);
    }
    public static object FromString(string typeName, string value)
    {
        if (typeName == string.Empty) return null;

        var serializer = GetSerializer(typeName) ?? throw new NotSupportedException($"Cannot read objects of type {typeName}");
        return serializer.FromString(value);
    }
    public static string ToString(Type type, object value)
    {
        if (value is null) return string.Empty;

        var typeName = type.FullName;

        var serializer = GetSerializer(typeName) ?? throw new NotSupportedException($"Cannot write objects of type {typeName}");
        return serializer.ToString(value);
    }
    public static ObjectSerializer GetSerializer(string typeName)
    {
        foreach (var serializer in serializers) if (serializer.CanSerialize(typeName)) return serializer;
        return null;
    }
    public static bool Supports(string typeName) => GetSerializer(typeName) is not null;
}
public class SimpleObjectSerializer<T>(Func<BinaryReader, object> read, Action<BinaryWriter, object> write, Func<string, object> fromString = null, Func<object, string> toString = null) : ObjectSerializer
{
    readonly Func<BinaryReader, object> read = read;
    readonly Action<BinaryWriter, object> write = write;
    readonly Func<string, object> fromString = fromString;
    readonly Func<object, string> toString = toString;

    public override bool CanSerialize(string typeName) => typeName == typeof(T).FullName;

    public override object ReadValue(BinaryReader reader) => read(reader);
    public override void WriteValue(BinaryWriter writer, object value) => write(writer, value);

    public override object FromString(string value) => fromString?.Invoke(value) ?? value;
    public override string ToString(object value) => toString?.Invoke(value) ?? (string)value;
}