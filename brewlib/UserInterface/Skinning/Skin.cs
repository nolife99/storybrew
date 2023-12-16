using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using BrewLib.Data;
using BrewLib.Graphics.Drawables;
using BrewLib.Graphics.Textures;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using Tiny;
using Tiny.Formats.Json;

namespace BrewLib.UserInterface.Skinning;

public sealed class Skin(TextureContainer textureContainer) : IDisposable
{
    public readonly TextureContainer TextureContainer = textureContainer;
    public Func<string, Type> ResolveDrawableType, ResolveWidgetType, ResolveStyleType;

    Dictionary<string, Drawable> drawables = [];
    readonly Dictionary<Type, Dictionary<string, WidgetStyle>> stylesPerType = [];

    public Drawable GetDrawable(string name)
    {
        if (drawables.TryGetValue(name, out var drawable)) return drawable;
        return NullDrawable.Instance;
    }

    public T GetStyle<T>(string name) where T : WidgetStyle => (T)GetStyle(typeof(T), name);
    public WidgetStyle GetStyle(Type type, string name)
    {
        name ??= "default";
        if (!stylesPerType.TryGetValue(type, out var styles)) return null;

        var n = name;
        while (n is not null)
        {
            if (styles.TryGetValue(n, out var style)) return style;
            n = getImplicitParentStyleName(n);
        }

        if (getBaseStyleName(name) != "default")
        {
            var flags = getStyleFlags(name);
            if (flags is not null) return GetStyle(type, $"default {flags}");
            return GetStyle(type, "default");
        }
        return null;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            drawables.Dispose();
            stylesPerType.Clear();

            drawables = null;
            disposed = true;
        }
    }

    #endregion

    #region Loading

    public void Load(string filename, ResourceContainer resourceContainer = null) => load(loadJson(filename, resourceContainer));
    void load(TinyObject data)
    {
        var constants = data.Value<TinyObject>("constants");

        loadDrawables(data.Value<TinyObject>("drawables"), constants);
        loadStyles(data.Value<TinyObject>("styles"), constants);
    }
    TinyObject loadJson(string filename, ResourceContainer resourceContainer)
    {
        TinyToken token = null;
        if (File.Exists(filename)) token = TinyToken.Read(filename);
        else
        {
            var data = resourceContainer?.GetString(filename, ResourceSource.Embedded);
            if (data is not null) token = TinyToken.ReadString<JsonFormat>(data);
        }

        if (token is TinyObject tinyObject) return resolveIncludes(tinyObject, resourceContainer);
        if (token is null) throw new FileNotFoundException(filename);
        throw new InvalidDataException($"{filename} does not contain an object");
    }
    TinyObject resolveIncludes(TinyObject data, ResourceContainer resourceContainer)
    {
        var includes = data.Value<TinyArray>("include");
        if (includes is not null)
        {
            var snapshot = includes.ToArray();
            for (var i = 0; i < snapshot.Length; ++i) data.Merge(loadJson(snapshot[i].Value<string>(), resourceContainer));
        }
        return data;
    }
    void loadDrawables(TinyObject data, TinyObject constants)
    {
        if (data is null) return;
        foreach (var entry in data)
        {
            var name = entry.Key;
            try
            {
                drawables.Add(name, loadDrawable(entry.Value, constants));
            }
            catch (TypeLoadException)
            {
                Trace.WriteLine($"Skin - Drawable type for {name} doesn't exist");
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Skin - Failed to load drawable {name}: {e}");
            }
        }
    }
    Drawable loadDrawable(TinyToken data, TinyObject constants)
    {
        if (data.Type is TinyTokenType.String)
        {
            var value = data.Value<string>();
            if (string.IsNullOrEmpty(value)) return NullDrawable.Instance;

            var drawable = GetDrawable(value);
            if (drawable == NullDrawable.Instance) throw new InvalidDataException($"Referenced drawable '{value}' must be defined before '{data}'");

            return drawable;
        }
        else if (data.Type is TinyTokenType.Array)
        {
            CompositeDrawable composite = new();
            foreach (var arrayDrawableData in data.Values<TinyToken>())
            {
                var drawable = loadDrawable(arrayDrawableData, constants);
                composite.Drawables.Add(drawable);
            }
            return composite;
        }
        else
        {
            var drawable = (Drawable)Activator.CreateInstance(ResolveDrawableType(data.Value<string>("_type") ?? throw new InvalidDataException($"Drawable '{data}' must declare a type")));
            parseFields(drawable, data.Value<TinyObject>(), null, constants);
            return drawable;
        }
    }
    void loadStyles(TinyObject data, TinyObject constants)
    {
        if (data is null) return;

        foreach (var styleTypeEntry in data)
        {
            var styleTypeName = styleTypeEntry.Key;
            var styleTypeObject = styleTypeEntry.Value.Value<TinyObject>();

            try
            {
                var widgetType = ResolveWidgetType(styleTypeName);
                var styleType = ResolveStyleType($"{styleTypeName}Style");

                if (!stylesPerType.TryGetValue(styleType, out var styles)) stylesPerType.Add(styleType, styles = []);

                WidgetStyle defaultStyle = null;
                foreach (var styleEntry in styleTypeObject)
                {
                    var styleName = styleEntry.Key;
                    var styleObject = styleEntry.Value.Value<TinyObject>();
                    try
                    {
                        var style = (WidgetStyle)Activator.CreateInstance(styleType);

                        var parentStyle = defaultStyle;
                        var implicitParentStyleName = getImplicitParentStyleName(styleName);
                        if (implicitParentStyleName is not null)
                        {
                            if (!styles.TryGetValue(implicitParentStyleName, out parentStyle) && styleTypeObject.Value<TinyToken>(implicitParentStyleName) is not null)
                                throw new InvalidDataException($"Implicit parent style '{implicitParentStyleName}' style must be defined before '{styleName}'");

                            parentStyle = GetStyle(styleType, implicitParentStyleName);
                        }

                        var parentName = styleObject.Value<string>("_parent");
                        if (parentName is not null && !styles.TryGetValue(parentName, out parentStyle))
                            throw new InvalidDataException($"Parent style '{parentName}' style must be defined before '{styleName}'");

                        parseFields(style, styleObject, parentStyle, constants);

                        if (defaultStyle is null)
                        {
                            if (styleName == "default") defaultStyle = style;
                            else throw new InvalidDataException($"The default {styleTypeName} style must be defined first");
                        }

                        styles.Add(styleName, style);
                    }
                    catch (InvalidDataException e)
                    {
                        Trace.WriteLine($"Skin - Invalid style {styleTypeName}.'{styleName}': {e.Message}");
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"Skin - Failed to load style {styleTypeName}.'{styleName}': {e}");
                    }
                }
            }
            catch (TypeLoadException)
            {
                Trace.WriteLine($"Skin - Widget type {styleTypeName} doesn't exist or isn't skinnable");
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Skin - Failed to load {styleTypeName} styles: {e}");
            }
        }
    }
    void parseFields(object skinnable, TinyObject data, object parent, TinyObject constants)
    {
        var type = skinnable.GetType();
        while (type != typeof(object))
        {
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                var fieldData = resolveConstants(data.Value<TinyToken>(field.Name), constants);
                if (fieldData is not null)
                {
                    var fieldType = field.FieldType;
                    var parser = getFieldParser(fieldType);
                    if (parser is not null)
                    {
                        var value = parser.Invoke(fieldData, constants, this);
                        field.SetValue(skinnable, value);
                    }
                    else Trace.WriteLine($"Skin - No parser for {fieldType}");
                }
                else if (parent is not null) field.SetValue(skinnable, field.GetValue(parent));
            }
            type = type.BaseType;
        }
    }
    static TinyToken resolveConstants(TinyToken fieldData, TinyObject constants)
    {
        while (fieldData is not null && fieldData.Type is TinyTokenType.String)
        {
            var fieldString = fieldData.Value<string>();
            if (fieldString.StartsWith('@'))
            {
                fieldData = constants?[fieldString[1..]];
                if (fieldData is null) throw new InvalidDataException($"Missing skin constant: {fieldString}");
            }
            else break;
        }
        return fieldData;
    }
    static T resolve<T>(TinyToken data, TinyObject constants) => resolveConstants(data, constants).Value<T>();

    static string getBaseStyleName(string styleName)
    {
        var index = styleName.IndexOf(' ');
        if (index == -1) return styleName;
        return styleName[..index];
    }
    static string getStyleFlags(string styleName)
    {
        var index = styleName.LastIndexOf(' ');
        if (index == -1) return null;
        return styleName.Substring(index + 1, styleName.Length - index - 1);
    }
    static string getImplicitParentStyleName(string styleName)
    {
        var index = styleName.LastIndexOf(' ');
        if (index == -1) return null;
        return styleName[..index];
    }
    static Func<TinyToken, TinyObject, Skin, object> getFieldParser(Type fieldType)
    {
        if (fieldType.IsEnum) return (data, constants, skin) => Enum.Parse(fieldType, data.Value<string>());

        while (fieldType != typeof(object))
        {
            if (fieldParsers.TryGetValue(fieldType, out var parser)) return parser;
            fieldType = fieldType.BaseType;
        }
        return null;
    }

    static readonly Dictionary<Type, Func<TinyToken, TinyObject, Skin, object>> fieldParsers = new()
    {
        [typeof(string)] = (data, constants, skin) => data.Value<string>(),
        [typeof(float)] = (data, constants, skin) => data.Value<float>(),
        [typeof(double)] = (data, constants, skin) => data.Value<double>(),
        [typeof(int)] = (data, constants, skin) => data.Value<int>(),
        [typeof(bool)] = (data, constants, skin) => data.Value<bool>(),
        [typeof(Texture2dRegion)] = (data, constants, skin) => skin.TextureContainer.Get(data.Value<string>()),
        [typeof(Drawable)] = (data, constants, skin) => skin.loadDrawable(data.Value<TinyToken>(), constants),
        [typeof(Vector2)] = (data, constants, skin) =>
        {
            if (data is TinyArray tinyArray) return new Vector2(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants));
            throw new InvalidDataException($"Incorrect vector2 format: {data}");
        },
        [typeof(Color)] = (data, constants, skin) =>
        {
            if (data.Type is TinyTokenType.String)
            {
                var value = data.Value<string>();
                if (value.StartsWith('#')) return ColorTranslator.FromHtml(value);

                var colorMethod = typeof(Color).GetMethod($"get_{value}");
                if (colorMethod?.ReturnType == typeof(Color)) return colorMethod.Invoke(null, null);
            }
            if (data is TinyArray tinyArray) return tinyArray.Count switch
            {
                3 => Color.FromArgb((int)(resolve<float>(tinyArray[0], constants) * 255), (int)(resolve<float>(tinyArray[1], constants) * 255), (int)(resolve<float>(tinyArray[2], constants) * 255)),
                _ => Color.FromArgb((int)(resolve<float>(tinyArray[3], constants) * 255), (int)(resolve<float>(tinyArray[0], constants) * 255), (int)(resolve<float>(tinyArray[1], constants) * 255), (int)(resolve<float>(tinyArray[2], constants) * 255)),
            };
            throw new InvalidDataException($"Incorrect color format: {data}");
        },
        [typeof(FourSide)] = (data, constants, skin) =>
        {
            if (data is TinyArray tinyArray) return tinyArray.Count switch
            {
                1 => new FourSide(resolve<float>(tinyArray[0], constants)),
                2 => new FourSide(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants)),
                3 => new FourSide(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants), resolve<float>(tinyArray[2], constants)),
                _ => new FourSide(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants), resolve<float>(tinyArray[2], constants), resolve<float>(tinyArray[3], constants)),
            };
            throw new InvalidDataException($"Incorrect four side format: {data}");
        }
    };

    #endregion
}