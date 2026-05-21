namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Numerics;

public abstract class ShaderUniform
{
    protected ShaderUniform(ShaderUniformInfo info)
    {
        Info = info;
    }

    public ShaderUniformInfo Info { get; }
    public string Name => Info.Name;
    public ShaderValueType Type => Info.Type;
    public bool IsActive => Info.IsActive;
    internal int Location => Info.Location;
}

public sealed class ShaderUniform<T> : ShaderUniform
{
    readonly IShaderProgramBackend program;

    bool hasValue;
    T lastValue;

    internal ShaderUniform(IShaderProgramBackend program, ShaderUniformInfo info)
        : base(info)
    {
        this.program = program;
    }

    public void Set(T value)
    {
        if (!IsActive) return;
        if (hasValue && EqualityComparer<T>.Default.Equals(lastValue, value)) return;

        program.SetUniform(this, value);
        lastValue = value;
        hasValue = true;
    }

    internal void Invalidate() => hasValue = false;
}

public static class ShaderUniformCompatibility
{
    public static bool IsCompatibleWith<T>(this ShaderValueType type)
    {
        var valueType = typeof(T);

        return type switch
        {
            ShaderValueType.Unknown => true,
            ShaderValueType.Int or ShaderValueType.Sampler1D or ShaderValueType.Sampler2D
                or ShaderValueType.Sampler3D or ShaderValueType.SamplerCube
                or ShaderValueType.Sampler1DArray or ShaderValueType.Sampler2DArray
                or ShaderValueType.SamplerCubeArray or ShaderValueType.SamplerBuffer
                => valueType == typeof(int),
            ShaderValueType.Bool => valueType == typeof(bool) || valueType == typeof(int),
            ShaderValueType.Float => valueType == typeof(float),
            ShaderValueType.FloatVec2 => valueType == typeof(Vector2),
            ShaderValueType.FloatVec3 => valueType == typeof(Vector3),
            ShaderValueType.FloatVec4 => valueType == typeof(Vector4),
            ShaderValueType.FloatMat4 => valueType == typeof(Matrix4x4),
            _ => true
        };
    }
}
