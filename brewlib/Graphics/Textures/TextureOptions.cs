namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Data;
using OpenTK.Graphics.OpenGL;
using Tiny;
using Tiny.Formats.Json;

public class TextureOptions : IEquatable<TextureOptions>
{
    public static readonly TextureOptions Default = new();

    static readonly Dictionary<Type, Func<TinyToken, object>> fieldParsers = new()
    {
        [typeof(string)] = data => data.Value<string>(),
        [typeof(float)] = data => data.Value<float>(),
        [typeof(double)] = data => data.Value<double>(),
        [typeof(int)] = data => data.Value<int>(),
        [typeof(bool)] = data => data.Value<bool>()
    };

    // Settings
    public bool Srgb = true, PreMultiply, GenerateMipmaps;

    // Parameters
    public int TextureLodBias;
    public TextureMagFilter TextureMagFilter = TextureMagFilter.Linear;
    public TextureMinFilter TextureMinFilter = TextureMinFilter.Linear;
    public TextureWrapMode TextureWrapS = TextureWrapMode.ClampToEdge, TextureWrapT = TextureWrapMode.ClampToEdge;

    public bool Equals(TextureOptions other) => Srgb == other.Srgb && GenerateMipmaps == other.GenerateMipmaps &&
        TextureLodBias == other.TextureLodBias && TextureMinFilter == other.TextureMinFilter &&
        TextureMagFilter == other.TextureMagFilter && TextureWrapS == other.TextureWrapS && TextureWrapT == other.TextureWrapT;

    public void ApplyParameters(TextureTarget target)
    {
        if (TextureLodBias != 0)
            GL.TexEnv(TextureEnvTarget.TextureFilterControl, TextureEnvParameter.TextureLodBias, TextureLodBias);

        GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int)TextureMinFilter);
        GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter);
        GL.TexParameter(target, TextureParameterName.TextureWrapS, (int)TextureWrapS);
        GL.TexParameter(target, TextureParameterName.TextureWrapT, (int)TextureWrapT);
    }

    public override bool Equals(object obj) => Equals(obj as TextureOptions);
    public override int GetHashCode()
        => HashCode.Combine(TextureLodBias, TextureMinFilter, TextureMagFilter, TextureWrapS, TextureWrapT);

    public static string GetOptionsFilename(string textureFilename) => Path.Combine(Path.GetDirectoryName(textureFilename),
        Path.GetFileNameWithoutExtension(textureFilename) + "-opt.json");

    public static TextureOptions Load(string filename, ResourceContainer resourceContainer = null)
    {
        TinyToken token = null;
        if (File.Exists(filename)) token = TinyToken.Read(filename);
        else
        {
            var data = resourceContainer?.GetString(filename, ResourceSource.Embedded);
            if (data is not null) token = TinyToken.ReadString<JsonFormat>(data);
        }

        return token is not null ? load(token) : null;
    }
    static TextureOptions load(TinyToken data)
    {
        TextureOptions options = new();
        parseFields(options, data);
        return options;
    }

    static void parseFields(object obj, TinyToken data)
    {
        var type = obj.GetType();
        while (type != typeof(object))
        {
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                var fieldData = data.Value<TinyToken>(field.Name);
                if (fieldData is not null)
                {
                    var parser = getFieldParser(fieldType);
                    if (parser is not null)
                    {
                        var value = parser.Invoke(fieldData);
                        field.SetValue(obj, value);
                    }
                    else Trace.TraceWarning($"No parser for {fieldType}");
                }
            }

            type = type.BaseType;
        }
    }
    static Func<TinyToken, object> getFieldParser(Type fieldType)
    {
        if (fieldType.IsEnum) return data => Enum.Parse(fieldType, data.Value<string>());
        while (fieldType != typeof(object))
        {
            if (fieldParsers.TryGetValue(fieldType, out var parser)) return parser;
            fieldType = fieldType.BaseType;
        }

        return null;
    }
}