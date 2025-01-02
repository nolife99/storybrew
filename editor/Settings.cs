namespace StorybrewEditor;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using BrewLib.IO;
using BrewLib.UserInterface;
using Storyboarding;
using StorybrewCommon.Util;

public class Settings
{
    public const string DefaultPath = "settings.cfg";

    public readonly Setting<bool> FitStoryboard = new(false), ShowStats = new(true), VerboseVsCode = new(false),
        TextureCompression = new(true);

    public readonly Setting<int> FrameRate = new(0), UpdateRate = new(0), EffectThreads = new(0);

    public readonly Setting<string> Id = new(Guid.NewGuid().ToString("N")), TimeCopyFormat = new(@"h\:mm\:ss\.ff");

    readonly string path;
    public readonly Setting<float> Volume = new(1);

    public Settings(string path = DefaultPath)
    {
        this.path = path;

        if (!File.Exists(path))
        {
            Save();
            return;
        }

        Trace.WriteLine($"Loading settings from '{path}'");

        var type = GetType();
        try
        {
            using var reader = File.OpenText(path);
            reader.ParseKeyValueSection((key, value) =>
            {
                var field = type.GetField(key);
                if (field is null ||
                    !field.FieldType.IsGenericType ||
                    !typeof(Setting).IsAssignableFrom(field.FieldType.GetGenericTypeDefinition())) return;

                try
                {
                    Unsafe.As<Setting>(field.GetValue(this)).Set(value);
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Loading setting {key} with value {value}: {e}");
                }
            });
        }
        catch (Exception e)
        {
            Trace.TraceError($"Loading settings: {e}");
            Save();
        }
    }

    public void Save()
    {
        Trace.WriteLine($"Saving settings at '{path}'");

        using SafeWriteStream stream = new(path);
        using StreamWriter writer = new(stream, Project.Encoding);

        foreach (var field in GetType().GetFields())
        {
            if (!field.FieldType.IsGenericType ||
                !typeof(Setting).IsAssignableFrom(field.FieldType.GetGenericTypeDefinition())) continue;

            writer.WriteLine($"{field.Name}: {Unsafe.As<Setting>(field.GetValue(this))}");
        }

        stream.Commit();
    }
}

public interface Setting
{
    void Set(object value);
}

public class Setting<T>(T defaultValue) : Setting
{
    T value = defaultValue;
    public void Set(object value) => Set((T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture));
    public event EventHandler OnValueChanged;

    public void Set(T value)
    {
        if (this.value.Equals(value)) return;

        this.value = value;
        OnValueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Bind(Field field, Action changedAction)
    {
        field.OnValueChanged += (_, _) => Set(field.FieldValue);
        EventHandler handler;
        OnValueChanged += handler = (_, _) =>
        {
            field.FieldValue = value;
            changedAction?.Invoke();
        };

        field.OnDisposed += (_, _) => OnValueChanged -= handler;
        handler(this, EventArgs.Empty);
    }

    public override string ToString() => typeof(T).GetInterface(nameof(IConvertible)) is not null ?
        Convert.ToString(value, CultureInfo.InvariantCulture) :
        value.ToString();

    public static implicit operator T(Setting<T> setting) => setting.value;
}