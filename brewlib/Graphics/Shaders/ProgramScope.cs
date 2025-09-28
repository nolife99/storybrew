namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using BrewLib.Util;
using osuTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Temporary;

public class ProgramScope
{
    readonly List<ShaderStorageType> ssbos = [];
    readonly List<ShaderType> structs = [];
    readonly List<ShaderVariable> varyings = [], uniforms = [], vertexBuiltins = [], fragmentBuiltins = [];

    int lastId, bindingIndex;
    string nextGenericTypeName => $"t{lastId++:000}";
    string nextGenericVaryingName => $"v{lastId++:000}";

    public ShaderType AddStruct()
    {
        ShaderType type = new(nextGenericTypeName);
        structs.Add(type);
        return type;
    }

    public ShaderStorageType AddSSBO()
    {
        ShaderStorageType type = new(nextGenericTypeName, bindingIndex++);
        ssbos.Add(type);
        return type;
    }

    public ShaderVariable AddUniform(ShaderContext context,
        string name,
        ActiveUniformType shaderTypeName,
        int count = -1)
    {
        ShaderVariable uniform = new(context, name, shaderTypeName, count);
        uniforms.Add(uniform);
        return uniform;
    }

    public ShaderVariable AddVarying(ShaderContext context, ActiveUniformType shaderTypeName)
    {
        ShaderVariable varying = new(context, nextGenericVaryingName, shaderTypeName);
        varyings.Add(varying);
        return varying;
    }

    public ShaderVariable AddBuiltinVarying(ShaderContext context,
        string name,
        ActiveUniformType shaderTypeName,
        bool isFragmentShader)
    {
        ShaderVariable varying = new(context, name, shaderTypeName);
        if (isFragmentShader) fragmentBuiltins.Add(varying);
        else vertexBuiltins.Add(varying);

        return varying;
    }

    public void DeclareTypes(scoped ref TempList<char> code)
    {
        foreach (var type in structs)
        {
            code.Append($"struct {type.Name} {{\n");
            foreach (var field in type.Fields) code.Append($"{field.ShaderTypeName.GetString()} {field.Name};\n");
            code.AddRange("};\n");
        }

        foreach (var type in ssbos)
        {
            code.Append(CultureInfo.InvariantCulture,
                $"layout(binding = {type.BindingIndex}) buffer {ShaderStorageType.BlockName} {{\n");

            foreach (var field in type.Fields)
            {
                code.Append($"{field.ShaderTypeName.GetString()} {field.Name}");

                if (field.ArrayCount == 0) code.AddRange("[];");
                else if (field.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{field.ArrayCount}];");

                code.Add('\n');
            }

            code.Append($"}} {type.Name};\n");
        }
    }

    public void DeclareUniforms(scoped ref TempList<char> code)
    {
        foreach (var uniform in uniforms)
        {
            code.Append($"uniform {uniform.ShaderTypeName.GetString()} {uniform.Name}");
            if (uniform.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{uniform.ArrayCount}]");
            code.AddRange(";\n");
        }
    }

    public void DeclareVaryings(scoped ref TempList<char> code, ShaderContext context, bool isFragmentShader)
    {
        if (isFragmentShader)
            for (var i = 0; i < fragmentBuiltins.Count; i++)
                DeclareBuiltinVarying(ref code, fragmentBuiltins[i]);
        else
            for (var i = 0; i < vertexBuiltins.Count; i++)
                DeclareBuiltinVarying(ref code, vertexBuiltins[i]);

        foreach (var varying in varyings)
            if (context.Uses(varying))
            {
                if (varying.ShaderTypeName.IsFlatType()) code.AddRange("flat ");
                code.Append($"{(isFragmentShader ? "in" : "out")} {varying.ShaderTypeName.GetString()} {varying.Name}");

                if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
                code.AddRange(";\n");
            }

        return;

        void DeclareBuiltinVarying(scoped ref TempList<char> code, ShaderVariable varying)
        {
            code.Append(CultureInfo.InvariantCulture, $"out {varying.ShaderTypeName.GetString()} {varying.Name}");

            if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
            code.AddRange(";\n");
        }
    }

    public void DeclareUnusedVaryingsAsVariables(scoped ref TempList<char> code, ShaderContext context)
    {
        foreach (var varying in varyings)
            if (!context.Uses(varying))
            {
                code.Append($"{varying.ShaderTypeName.GetString()} {varying.Name}");
                if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
                code.AddRange(";\n");
            }
    }
}