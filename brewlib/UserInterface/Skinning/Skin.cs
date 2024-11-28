namespace BrewLib.UserInterface.Skinning;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Data;
using Graphics.Drawables;
using Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Styles;
using Tiny;
using Tiny.Formats.Json;
using Util;

public sealed class Skin(TextureContainer textureContainer) : IDisposable
{
    readonly Dictionary<string, Drawable> drawables = [];
    readonly Dictionary<Type, Dictionary<string, WidgetStyle>> stylesPerType = [];
    readonly TextureContainer TextureContainer = textureContainer;
    public Func<string, Type> ResolveDrawableType, ResolveWidgetType, ResolveStyleType;

    public Drawable GetDrawable(string name) => drawables.TryGetValue(name, out var drawable) ? drawable : NullDrawable.Instance;

    public T GetStyle<T>(string name) where T : WidgetStyle => (T)GetStyle(typeof(T), name);
    WidgetStyle GetStyle(Type type, string name)
    {
        while (true)
        {
            name ??= "default";
            if (!stylesPerType.TryGetValue(type, out var styles)) return null;

            var n = name;
            while (n is not null)
            {
                if (styles.TryGetValue(n, out var style)) return style;
                n = getImplicitParentStyleName(n);
            }

            if (getBaseStyleName(name) == "default") return null;

            var flags = getStyleFlags(name);
            name = flags is not null ? $"default {flags}" : "default";
        }
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        drawables.Dispose();
        disposed = true;
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

        return token switch
        {
            TinyObject tinyObject => resolveIncludes(tinyObject, resourceContainer),
            null => throw new FileNotFoundException(filename),
            _ => throw new InvalidDataException($"{filename} does not contain an object")
        };
    }
    TinyObject resolveIncludes(TinyObject data, ResourceContainer resourceContainer)
    {
        var includes = data.Value<TinyArray>("include");
        if (includes is null) return data;

        var snapshot = includes.ToArray();
        foreach (var t in snapshot) data.Merge(loadJson(t.Value<string>(), resourceContainer));

        return data;
    }
    void loadDrawables(TinyObject data, TinyObject constants)
    {
        if (data is null) return;
        foreach (var (name, value) in data)
            try
            {
                drawables.Add(name, loadDrawable(value, constants));
            }
            catch (TypeLoadException)
            {
                Trace.TraceWarning($"Skin - Drawable type for {name} doesn't exist");
            }
            catch (Exception e)
            {
                Trace.TraceError($"Skin - Loading drawable {name}: {e}");
            }
    }
    Drawable loadDrawable(TinyToken data, TinyObject constants)
    {
        switch (data.Type)
        {
            case TinyTokenType.String:
            {
                var value = data.Value<string>();
                if (string.IsNullOrEmpty(value)) return NullDrawable.Instance;

                var drawable = GetDrawable(value);
                if (drawable == NullDrawable.Instance)
                    throw new InvalidDataException($"Referenced drawable '{value}' must be defined before '{data}'");

                return drawable;
            }
            case TinyTokenType.Array:
            {
                CompositeDrawable composite = new();
                foreach (var arrayDrawableData in data.Values<TinyToken>())
                {
                    var drawable = loadDrawable(arrayDrawableData, constants);
                    composite.Drawables.Add(drawable);
                }

                return composite;
            }
        }

        var drawableB = Unsafe.As<Drawable>(Activator.CreateInstance(ResolveDrawableType(data.Value<string>("_type")) ??
            throw new InvalidDataException($"Drawable '{data}' must declare a type")));

        parseFields(drawableB, data.Value<TinyObject>(), null, constants);
        return drawableB;
    }
    void loadStyles(TinyObject data, TinyObject constants)
    {
        if (data is null) return;
        foreach (var (styleTypeName, value) in data)
        {
            var styleTypeObject = value.Value<TinyObject>();
            try
            {
                var styleType = ResolveStyleType(styleTypeName + "Style");
                if (!stylesPerType.TryGetValue(styleType, out var styles)) stylesPerType[styleType] = styles = [];

                WidgetStyle defaultStyle = null;
                foreach (var (styleName, tinyToken) in styleTypeObject)
                {
                    var styleObject = tinyToken.Value<TinyObject>();
                    try
                    {
                        var style = Unsafe.As<WidgetStyle>(Activator.CreateInstance(styleType));

                        var parentStyle = defaultStyle;
                        var implicitParentStyleName = getImplicitParentStyleName(styleName);
                        if (implicitParentStyleName is not null)
                        {
                            if (!styles.TryGetValue(implicitParentStyleName, out parentStyle) &&
                                styleTypeObject.Value<TinyToken>(implicitParentStyleName) is not null)
                                throw new InvalidDataException(
                                    $"Implicit parent style '{implicitParentStyleName}' style must be defined before '{styleName}'");

                            parentStyle = GetStyle(styleType, implicitParentStyleName);
                        }

                        var parentName = styleObject.Value<string>("_parent");
                        if (parentName is not null && !styles.TryGetValue(parentName, out parentStyle))
                            throw new InvalidDataException(
                                $"Parent style '{parentName}' style must be defined before '{styleName}'");

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
                        Trace.TraceWarning($"Skin - Invalid style {styleTypeName}.'{styleName}': {e.Message}");
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError($"Skin - Loading style {styleTypeName}.'{styleName}': {e}");
                    }
                }
            }
            catch (TypeLoadException)
            {
                Trace.TraceWarning($"Skin - Widget type {styleTypeName} doesn't exist or isn't skinnable");
            }
            catch (Exception e)
            {
                Trace.TraceError($"Skin - Loading {styleTypeName} styles: {e}");
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
                    else Trace.TraceWarning($"Skin - No parser for {fieldType}");
                }
                else if (parent is not null) field.SetValue(skinnable, field.GetValue(parent));
            }

            type = type.BaseType;
        }
    }
    static TinyToken resolveConstants(TinyToken fieldData, TinyObject constants)
    {
        while (fieldData?.Type is TinyTokenType.String)
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
        return index == -1 ? styleName : styleName[..index];
    }
    static string getStyleFlags(string styleName)
    {
        var index = styleName.LastIndexOf(' ');
        return index == -1 ? null : styleName.Substring(index + 1, styleName.Length - index - 1);
    }
    static string getImplicitParentStyleName(string styleName)
    {
        var index = styleName.LastIndexOf(' ');
        return index == -1 ? null : styleName[..index];
    }
    static Func<TinyToken, TinyObject, Skin, object> getFieldParser(Type fieldType)
    {
        if (fieldType.IsEnum) return (data, _, _) => Enum.Parse(fieldType, data.Value<string>());
        while (fieldType != typeof(object))
        {
            var parser = fieldParsers.GetValueRefOrNullRef(fieldType);
            if (parser is not null) return parser;
            fieldType = fieldType.BaseType;
        }

        return null;
    }

    static readonly FrozenDictionary<Type, Func<TinyToken, TinyObject, Skin, object>> fieldParsers =
        new Dictionary<Type, Func<TinyToken, TinyObject, Skin, object>>
        {
            [typeof(string)] = (data, _, _) => data.Value<string>(),
            [typeof(float)] = (data, _, _) => data.Value<float>(),
            [typeof(double)] = (data, _, _) => data.Value<double>(),
            [typeof(int)] = (data, _, _) => data.Value<int>(),
            [typeof(bool)] = (data, _, _) => data.Value<bool>(),
            [typeof(Texture2dRegion)] = (data, _, skin) => skin.TextureContainer.Get(data.Value<string>()),
            [typeof(Drawable)] = (data, constants, skin) => skin.loadDrawable(data.Value<TinyToken>(), constants),
            [typeof(Vector2)] = (data, constants, _) =>
            {
                if (data is TinyArray tinyArray)
                    return new Vector2(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants));

                throw new InvalidDataException($"Incorrect vector2 format: {data}");
            },
            [typeof(Rgba32)] = (data, constants, _) =>
            {
                if (data.Type is TinyTokenType.String)
                {
                    var value = data.Value<string>();
                    if (value.StartsWith('#')) return Rgba32.ParseHex(value);

                    var colorField = typeof(Color).GetField(value);
                    if (colorField?.FieldType == typeof(Color)) return (Rgba32)Unsafe.Unbox<Color>(colorField.GetValue(null));
                }

                if (data is TinyArray tinyArray)
                    return tinyArray.Count switch
                    {
                        3 => new Rgba32(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants),
                            resolve<float>(tinyArray[2], constants)),
                        _ => new Rgba32(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants),
                            resolve<float>(tinyArray[2], constants), resolve<float>(tinyArray[3], constants))
                    };

                throw new InvalidDataException($"Incorrect color format: {data}");
            },
            [typeof(FourSide)] = (data, constants, _) =>
            {
                if (data is TinyArray tinyArray)
                    return tinyArray.Count switch
                    {
                        1 => new FourSide(resolve<float>(tinyArray[0], constants)),
                        2 => new FourSide(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants)),
                        3 => new FourSide(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants),
                            resolve<float>(tinyArray[2], constants)),
                        _ => new FourSide(resolve<float>(tinyArray[0], constants), resolve<float>(tinyArray[1], constants),
                            resolve<float>(tinyArray[2], constants), resolve<float>(tinyArray[3], constants))
                    };

                throw new InvalidDataException($"Incorrect four side format: {data}");
            }
        }.ToFrozenDictionary();

    #endregion
}