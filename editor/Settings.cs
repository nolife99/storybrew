using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using BrewLib.UserInterface;
using BrewLib.Util;
using StorybrewCommon.Util;
using StorybrewEditor.Storyboarding;

namespace StorybrewEditor;

public class Settings
{
    public const string DefaultPath = "settings.cfg";

    public readonly Setting<string> Id = new(Guid.NewGuid().ToString("N")), TimeCopyFormat = new(@"h\:mm\:ss\.ff");
    public readonly Setting<int> FrameRate = new(0), UpdateRate = new(60), EffectThreads = new(0);
    public readonly Setting<float> Volume = new(.5f);
    public readonly Setting<bool> FitStoryboard = new(false), ShowStats = new(true),
        VerboseVsCode = new(false), UseRoslyn = new(false);

    readonly string path;

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
                if (field is null || !field.FieldType.IsGenericType || !typeof(Setting).IsAssignableFrom(field.FieldType.GetGenericTypeDefinition())) return;

                try
                {
                    var setting = (Setting)field.GetValue(this);
                    setting.Set(value);
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Failed to load setting {key} with value {value}: {e}");
                }
            });
        }
        catch (Exception e)
        {
            Trace.TraceError($"Failed to load settings: {e}");
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
            if (!field.FieldType.IsGenericType || !typeof(Setting).IsAssignableFrom(field.FieldType.GetGenericTypeDefinition())) continue;

            var setting = (Setting)field.GetValue(this);
            writer.WriteLine($"{field.Name}: {setting}");
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
    public event EventHandler OnValueChanged;

    public void Set(T value)
    {
        if (this.value.Equals(value)) return;
        this.value = value;
        OnValueChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Set(object value) => Set((T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture));

    public void Bind(Field field, Action changedAction)
    {
        field.OnValueChanged += (sender, e) => Set(field.FieldValue);
        EventHandler handler;
        OnValueChanged += handler = (sender, e) =>
        {
            field.FieldValue = value;
            changedAction?.Invoke();
        };
        field.OnDisposed += (sender, e) => OnValueChanged -= handler;
        handler(this, EventArgs.Empty);
    }

    public static implicit operator T(Setting<T> setting) => setting.value;

    public override string ToString()
    {
        if (typeof(T).GetInterface(nameof(IConvertible)) is not null) return Convert.ToString(value, CultureInfo.InvariantCulture);
        return value.ToString();
    }
}
public static class SettingsExtensions
{
    public static void BindToSetting<T>(this Button button, Setting<T> setting, Action changedAction)
    {
        button.OnValueChanged += (sender, e) => setting.Set(button.Checked);
        EventHandler handler;
        setting.OnValueChanged += handler = (sender, e) =>
        {
            button.Checked = (bool)Convert.ChangeType((T)setting, typeof(bool), CultureInfo.InvariantCulture);
            changedAction?.Invoke();
        };
        button.OnDisposed += (sender, e) => setting.OnValueChanged -= handler;
        handler(button, EventArgs.Empty);
    }
}