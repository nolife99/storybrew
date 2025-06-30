namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Temporary;
using Util;

public class ProgramScope
{
    readonly List<ShaderStorageType> ssbos = [];
    readonly List<ShaderType> structs = [];
    readonly List<ShaderVariable> varyings = [], uniforms = [], vertexBuiltins = [], fragmentBuiltins = [];

    int lastId;
    string nextGenericTypeName => $"t{lastId++:000}";
    string nextGenericVaryingName => $"v{lastId++:000}";

    public ShaderType AddStruct()
    {
        ShaderType type = new(nextGenericTypeName);
        structs.Add(type);
        return type;
    }

    public ShaderStorageType AddSSBO(int bindingIndex)
    {
        ShaderStorageType type = new(nextGenericTypeName, bindingIndex);
        ssbos.Add(type);
        return type;
    }

    public ShaderVariable AddUniform(ShaderContext context, string name, ActiveUniformType shaderTypeName, int count = -1)
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
            code.Append("struct ");
            code.Append(type.Name);
            code.Append(" {\n");

            foreach (var field in type.Fields)
            {
                code.AddRange(field.ShaderTypeName.GetString());
                code.Add(' ');
                code.Append(field.Name);
                code.Append(";\n");
            }

            code.Append("};\n");
        }

        foreach (var type in ssbos)
        {
            code.Append("layout(binding = ");
            code.AppendFormatted(type.BindingIndex, provider: CultureInfo.InvariantCulture);
            code.Append(") buffer ");
            code.Append(ShaderStorageType.BlockName);
            code.Append(" {\n");

            foreach (var field in type.Fields)
            {
                code.AddRange(field.ShaderTypeName.GetString());
                code.Add(' ');
                code.Append(field.Name);

                if (field.ArrayCount == 0) code.Append("[];");
                else if (field.ArrayCount != -1)
                {
                    code.Add('[');
                    code.AppendFormatted(field.ArrayCount, provider: CultureInfo.InvariantCulture);
                    code.Append("];");
                }

                code.Add('\n');
            }

            code.Append("} ");
            code.Append(type.Name);
            code.Append(";\n");
        }
    }

    public void DeclareUniforms(scoped ref TempList<char> code)
    {
        foreach (var uniform in uniforms)
        {
            code.Append("uniform ");
            code.AddRange(uniform.ShaderTypeName.GetString());
            code.Add(' ');
            code.Append(uniform.Name);

            if (uniform.ArrayCount != -1)
            {
                code.Add('[');
                code.AppendFormatted(uniform.ArrayCount, provider: CultureInfo.InvariantCulture);
                code.Add(']');
            }

            code.Append(";\n");
        }
    }

    public void DeclareVaryings(scoped ref TempList<char> code, ShaderContext context, bool isFragmentShader)
    {
        if (isFragmentShader)
            for (var i = 0; i < fragmentBuiltins.Count; i++)
                DeclareBuiltinVarying(ref code, fragmentBuiltins[i], i);
        else
            for (var i = 0; i < vertexBuiltins.Count; i++)
                DeclareBuiltinVarying(ref code, vertexBuiltins[i], i);

        foreach (var varying in varyings)
            if (context.Uses(varying))
            {
                if (varying.ShaderTypeName.IsFlatType()) code.Append("flat ");
                code.Append(isFragmentShader ? "in" : "out");
                code.Add(' ');
                code.AddRange(varying.ShaderTypeName.GetString());
                code.Add(' ');
                code.Append(varying.Name);

                if (varying.ArrayCount != -1)
                {
                    code.Add('[');
                    code.AppendFormatted(varying.ArrayCount, provider: CultureInfo.InvariantCulture);
                    code.Add(']');
                }

                code.Append(";\n");
            }

        return;

        void DeclareBuiltinVarying(scoped ref TempList<char> code, ShaderVariable varying, int index)
        {
            code.Append("layout(location = ");
            code.AppendFormatted(index, provider: CultureInfo.InvariantCulture);
            code.Append(") out ");
            code.AddRange(varying.ShaderTypeName.GetString());
            code.Add(' ');
            code.Append(varying.Name);

            if (varying.ArrayCount != -1)
            {
                code.Add('[');
                code.AppendFormatted(varying.ArrayCount, provider: CultureInfo.InvariantCulture);
                code.Add(']');
            }

            code.Append(";\n");
        }
    }

    public void DeclareUnusedVaryingsAsVariables(scoped ref TempList<char> code, ShaderContext context)
    {
        foreach (var varying in varyings)
            if (!context.Uses(varying))
            {
                code.AddRange(varying.ShaderTypeName.GetString());
                code.Add(' ');
                code.Append(varying.Name);

                if (varying.ArrayCount != -1)
                {
                    code.Add('[');
                    code.AppendFormatted(varying.ArrayCount, provider: CultureInfo.InvariantCulture);
                    code.Add(']');
                }

                code.Append(";\n");
            }
    }
}