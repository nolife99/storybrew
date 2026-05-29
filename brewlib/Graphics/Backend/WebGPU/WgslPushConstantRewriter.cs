namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;

/// <summary>
///     Rewrites the conventional uniform block declaration in a WGSL shader so it uses wgpu-native's
///     <c>var&lt;immediate&gt;</c> address space instead of a uniform buffer binding.
///     The renderers in this project always emit a uniform block at <c>@group(0) @binding(0)</c> when they have
///     a single transform uniform. This rewriter detects that exact pattern and replaces it. If the pattern doesn't
///     match (for example, the renderer uses multiple uniforms or a different group/binding), the source is returned
///     unchanged and the backend falls back to a dynamic-offset uniform buffer.
///     The rewrite is intentionally conservative — it only touches declarations that match the simple grammar
///     <c>@group(0) @binding(0) var&lt;uniform&gt; name : Type ;</c> (whitespace flexible) and won't fire on anything more
///     complex. False negatives are safe (we just take the slower path); false positives would produce invalid WGSL.
/// </summary>
static class WgslPushConstantRewriter
{
    public static RewriteResult TryRewrite(string source)
    {
        if (string.IsNullOrEmpty(source))
            return new(source, null, null, false);

        // Walk the source looking for the well-known declaration shape. We need to be careful not to match
        // inside comments or strings; for the renderer-generated shaders in this project that's not a concern,
        // but to be defensive we strip line comments before searching.
        var stripped = StripLineComments(source);

        var match = FindUniformDeclaration(stripped);
        if (!match.HasValue) return new(source, null, null, false);

        var (start, end, name, type) = match.Value;

        // Map the location in the stripped source back to the original — they differ in whitespace only because
        // we replaced comments with spaces of equal length, preserving offsets.
        var replacement = $"var<immediate> {name}: {type};";
        var rewritten = string.Concat(source.AsSpan(0, start), replacement, source.AsSpan(end));

        return new(rewritten, name, type, true);
    }

    static string StripLineComments(string source)
    {
        var chars = source.ToCharArray();
        for (var i = 0; i < chars.Length; ++i)
        {
            if (i + 1 < chars.Length && chars[i] == '/' && chars[i + 1] == '/')
            {
                while (i < chars.Length && chars[i] != '\n')
                {
                    chars[i] = ' ';
                    ++i;
                }
            }
            else if (i + 1 < chars.Length && chars[i] == '/' && chars[i + 1] == '*')
            {
                while (i + 1 < chars.Length && !(chars[i] == '*' && chars[i + 1] == '/'))
                {
                    if (chars[i] != '\n') chars[i] = ' ';
                    ++i;
                }

                if (i + 1 < chars.Length)
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    ++i;
                }
            }
        }

        return new(chars);
    }

    /// <summary>
    ///     Locate one <c>@group(0) @binding(0) var&lt;uniform&gt; name : Type ;</c> declaration. The two attributes may
    ///     appear in either order.
    /// </summary>
    static (int Start, int End, string Name, string Type)? FindUniformDeclaration(string s)
    {
        // We scan for '@group(0)' or '@binding(0)' as the anchor; the surrounding token must form a valid pair.
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] != '@')
            {
                ++i;
                continue;
            }

            var declStart = i;
            int afterAttrs;

            // First attribute
            if (!TryParseAttribute(s, i, out var attr1, out var arg1, out afterAttrs))
            {
                ++i;
                continue;
            }

            // Skip whitespace
            var j = SkipWhitespace(s, afterAttrs);
            if (j >= s.Length || s[j] != '@')
            {
                ++i;
                continue;
            }

            if (!TryParseAttribute(s, j, out var attr2, out var arg2, out afterAttrs))
            {
                ++i;
                continue;
            }

            // We need one '@group(0)' and one '@binding(0)' in either order
            uint? group = null, binding = null;
            switch (attr1)
            {
                case "group": group = arg1; break;
                case "binding": binding = arg1; break;

                default:
                    ++i;
                    continue;
            }

            switch (attr2)
            {
                case "group": group = arg2; break;
                case "binding": binding = arg2; break;

                default:
                    ++i;
                    continue;
            }

            if (group != 0u || binding != 0u)
            {
                ++i;
                continue;
            }

            // Look for: var<uniform> name : Type ;
            j = SkipWhitespace(s, afterAttrs);
            if (!ConsumeKeyword(s, ref j, "var"))
            {
                ++i;
                continue;
            }

            j = SkipWhitespace(s, j);
            if (j >= s.Length || s[j] != '<')
            {
                ++i;
                continue;
            }

            ++j;
            j = SkipWhitespace(s, j);
            if (!ConsumeKeyword(s, ref j, "uniform"))
            {
                ++i;
                continue;
            }

            j = SkipWhitespace(s, j);
            if (j >= s.Length || s[j] != '>')
            {
                ++i;
                continue;
            }

            ++j;
            j = SkipWhitespace(s, j);

            // Identifier
            var nameStart = j;
            while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_')) ++j;
            if (j == nameStart)
            {
                ++i;
                continue;
            }

            var name = s.Substring(nameStart, j - nameStart);

            j = SkipWhitespace(s, j);
            if (j >= s.Length || s[j] != ':')
            {
                ++i;
                continue;
            }

            ++j;
            j = SkipWhitespace(s, j);

            // Type — read until ';', tracking template angle brackets so we don't stop mid-type.
            var typeStart = j;
            var depth = 0;
            while (j < s.Length)
            {
                var ch = s[j];
                if (ch == '<') ++depth;
                else if (ch == '>') --depth;
                else if (ch == ';' && depth <= 0) break;

                ++j;
            }

            if (j >= s.Length || s[j] != ';')
            {
                ++i;
                continue;
            }

            var type = s.Substring(typeStart, j - typeStart).Trim();
            ++j; // consume ';'

            return (declStart, j, name, type);
        }

        return null;
    }

    static bool TryParseAttribute(string s, int start, out string name, out uint argument, out int end)
    {
        name = null;
        argument = 0;
        end = start;
        if (start >= s.Length || s[start] != '@') return false;

        var i = start + 1;
        var ns = i;
        while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '_')) ++i;
        if (i == ns) return false;

        var attr = s.Substring(ns, i - ns);
        i = SkipWhitespace(s, i);
        if (i >= s.Length || s[i] != '(') return false;

        ++i;
        i = SkipWhitespace(s, i);
        // Parse an unsigned integer literal
        var numStart = i;
        while (i < s.Length && char.IsDigit(s[i])) ++i;
        if (i == numStart) return false;
        if (!uint.TryParse(s.AsSpan(numStart, i - numStart), out var value)) return false;

        i = SkipWhitespace(s, i);
        if (i >= s.Length || s[i] != ')') return false;

        ++i;
        name = attr;
        argument = value;
        end = i;
        return true;
    }

    static int SkipWhitespace(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) ++i;
        return i;
    }

    static bool ConsumeKeyword(string s, ref int i, string keyword)
    {
        if (i + keyword.Length > s.Length) return false;

        for (var k = 0; k < keyword.Length; ++k)
            if (s[i + k] != keyword[k])
                return false;

        // Ensure the keyword isn't a prefix of a larger identifier
        var after = i + keyword.Length;
        if (after < s.Length && (char.IsLetterOrDigit(s[after]) || s[after] == '_')) return false;

        i = after;
        return true;
    }

    /// <summary>
    ///     Conservative byte-size estimate for the WGSL types the renderers actually use. Returns 0 if unknown
    ///     (caller should treat as "doesn't fit in push constants"). The naming convention matches what the
    ///     renderer WGSL declarations produce.
    /// </summary>
    public static uint EstimateTypeSize(string typeName,
        IReadOnlyDictionary<string, uint> structSizes = null)
    {
        if (string.IsNullOrEmpty(typeName)) return 0;

        var t = typeName.Trim();

        // vec/mat scalar types
        switch (t)
        {
            case "f32":
            case "i32":
            case "u32": return 4;

            case "vec2<f32>":
            case "vec2<i32>":
            case "vec2<u32>": return 8;

            case "vec3<f32>":
            case "vec3<i32>":
            case "vec3<u32>": return 16; // vec3 is padded to 16 in std140-like layout

            case "vec4<f32>":
            case "vec4<i32>":
            case "vec4<u32>": return 16;

            case "mat2x2<f32>": return 16;
            case "mat3x3<f32>": return 48;
            case "mat4x4<f32>": return 64;
            case "mat3x2<f32>": return 24;
            case "mat4x2<f32>": return 32;
        }

        // Look up by user-defined struct name when caller supplied a table
        if (structSizes is not null && structSizes.TryGetValue(t, out var size))
            return size;

        return 0;
    }

    /// <summary>
    ///     Crude WGSL struct-size scan for the renderer-generated shaders. Recognises only the shapes used in this
    ///     codebase (a single struct with a flat list of named fields, or one <c>array&lt;T, N&gt;</c> field).
    /// </summary>
    public static IReadOnlyDictionary<string, uint> ScanStructSizes(string source)
    {
        var result = new Dictionary<string, uint>(StringComparer.Ordinal);
        var i = 0;
        while (i < source.Length)
        {
            // Find "struct"
            var idx = source.IndexOf("struct", i, StringComparison.Ordinal);
            if (idx < 0) break;

            // Ensure word boundary
            if (idx > 0 && (char.IsLetterOrDigit(source[idx - 1]) || source[idx - 1] == '_'))
            {
                i = idx + 1;
                continue;
            }

            var after = idx + "struct".Length;
            if (after < source.Length && (char.IsLetterOrDigit(source[after]) || source[after] == '_'))
            {
                i = idx + 1;
                continue;
            }

            var p = SkipWs(source, after);
            var nameStart = p;
            while (p < source.Length && (char.IsLetterOrDigit(source[p]) || source[p] == '_')) ++p;
            if (p == nameStart)
            {
                i = idx + 1;
                continue;
            }

            var structName = source.Substring(nameStart, p - nameStart);

            p = SkipWs(source, p);
            if (p >= source.Length || source[p] != '{')
            {
                i = idx + 1;
                continue;
            }

            ++p;

            // Compute size by walking fields up to '}'
            uint size = 0;
            var sane = true;
            while (p < source.Length && source[p] != '}')
            {
                p = SkipWs(source, p);
                if (p < source.Length && source[p] == '}') break;

                // Skip optional attributes like @align(16) etc.
                while (p < source.Length && source[p] == '@')
                {
                    while (p < source.Length && source[p] != ')') ++p;
                    if (p < source.Length) ++p;
                    p = SkipWs(source, p);
                }

                // Field name
                while (p < source.Length && (char.IsLetterOrDigit(source[p]) || source[p] == '_')) ++p;
                p = SkipWs(source, p);
                if (p >= source.Length || source[p] != ':')
                {
                    sane = false;
                    break;
                }

                ++p;
                p = SkipWs(source, p);
                // Field type — read up to ',' or '}' (tracking angle brackets)
                var tStart = p;
                var depth = 0;
                while (p < source.Length)
                {
                    var c = source[p];
                    if (c == '<') ++depth;
                    else if (c == '>') --depth;
                    else if ((c == ',' || c == '}') && depth <= 0) break;

                    ++p;
                }

                var rawType = source.Substring(tStart, p - tStart).Trim();
                if (rawType.EndsWith(","))
                    rawType = rawType[..^1].Trim();

                size += FieldSize(rawType, result);
                if (p < source.Length && source[p] == ',') ++p;
            }

            if (sane && size > 0)
                result[structName] = size;

            i = p + 1;
        }

        return result;
    }

    static uint FieldSize(string typeText, IReadOnlyDictionary<string, uint> known)
    {
        if (string.IsNullOrEmpty(typeText)) return 0;

        var t = typeText.Trim();

        // array<T, N>
        const string arrayPrefix = "array<";
        if (t.StartsWith(arrayPrefix, StringComparison.Ordinal) && t.EndsWith(">", StringComparison.Ordinal))
        {
            var inner = t.Substring(arrayPrefix.Length, t.Length - arrayPrefix.Length - 1);
            var comma = SplitTopLevelComma(inner);
            if (comma < 0) return 0;

            var elemType = inner[..comma].Trim();
            if (!uint.TryParse(inner[(comma + 1)..].Trim(), out var count)) return 0;

            var elemSize = EstimateTypeSize(elemType, known);
            return elemSize * count;
        }

        return EstimateTypeSize(t, known);
    }

    static int SplitTopLevelComma(string s)
    {
        var depth = 0;
        for (var i = 0; i < s.Length; ++i)
        {
            var c = s[i];
            if (c == '<') ++depth;
            else if (c == '>') --depth;
            else if (c == ',' && depth == 0) return i;
        }

        return -1;
    }

    static int SkipWs(string s, int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) ++i;
        return i;
    }

    /// <summary>
    ///     Describes one uniform that was found at group 0 / binding 0 and rewritten.
    /// </summary>
    public readonly record struct RewriteResult(string TransformedSource,
        string UniformName,
        string UniformTypeName,
        bool Rewritten);
}