using StorybrewCommon.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace StorybrewCommon.Storyboarding
{
#pragma warning disable CS1591
    public class EffectConfig : MarshalByRefObject
    {
        readonly Dictionary<string, ConfigField> fields = new Dictionary<string, ConfigField>();

        public int FieldCount => fields.Count;
        public IEnumerable<ConfigField> Fields => fields.Values;
        public IEnumerable<ConfigField> SortedFields => new SortedSet<ConfigField>(fields.Values, Comparer<ConfigField>.Create((a, b) => a.Order - b.Order));
        public string[] FieldNames
        {
            get
            {
                var names = new string[fields.Count];
                fields.Keys.CopyTo(names, 0);
                return names;
            }
        }

        public void UpdateField(string name, string displayName, string description, int order, Type fieldType, object defaultValue, NamedValue[] allowedValues, string beginsGroup)
        {
            if (fieldType is null) return;
            if (displayName is null)
            {
                displayName = Regex.Replace(name, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2");
                displayName = Regex.Replace(displayName, @"(\p{Ll})(\P{Ll})", "$1 $2");
            }

            var value = fields.TryGetValue(name, out ConfigField field) ?
                convertFieldValue(field.Value, field.Type, fieldType, defaultValue) : defaultValue;

            var isAllowed = allowedValues is null;
            if (!isAllowed) for (var i = 0; i < allowedValues.Length; ++i) if (value.Equals(allowedValues[i].Value))
            {
                isAllowed = true;
                break;
            }
            if (!isAllowed) value = defaultValue;

            fields[name] = new ConfigField
            {
                Name = name,
                DisplayName = displayName,
                Description = description?.Trim(),
                Value = value,
                Type = fieldType,
                AllowedValues = allowedValues,
                BeginsGroup = beginsGroup,
                Order = order
            };
        }
        public void RemoveField(string name) => fields.Remove(name);

        public bool SetValue(string name, object value)
        {
            var field = fields[name];
            if (field.Value.Equals(value)) return false;

            fields[name] = new ConfigField
            {
                Name = field.Name,
                DisplayName = field.DisplayName,
                Description = field.Description,
                Value = value,
                Type = field.Type,
                AllowedValues = field.AllowedValues,
                BeginsGroup = field.BeginsGroup,
                Order = field.Order
            };
            return true;
        }
        public object GetValue(string name) => fields[name].Value;

        static object convertFieldValue(object value, Type oldType, Type newType, object defaultValue)
        {
            if (newType.IsAssignableFrom(oldType)) return value;
            try
            {
                return Convert.ChangeType(value, newType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        public struct ConfigField
        {
            public string Name, DisplayName, Description, BeginsGroup;
            public object Value;
            public Type Type;
            public NamedValue[] AllowedValues;
            public int Order;
        }
    }
}