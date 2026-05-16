namespace BrewLib.Graphics.Shaders;

using System;

public static class ShaderUtil
{
    public static ReadOnlySpan<char> GetString(this ShaderValueType type)
        => type switch
        {
            ShaderValueType.Int => "int",
            ShaderValueType.UnsignedInt => "uint",
            ShaderValueType.Float => "float",
            ShaderValueType.Double => "double",
            ShaderValueType.FloatVec2 => "vec2",
            ShaderValueType.FloatVec3 => "vec3",
            ShaderValueType.FloatVec4 => "vec4",
            ShaderValueType.IntVec2 => "ivec2",
            ShaderValueType.IntVec3 => "ivec3",
            ShaderValueType.IntVec4 => "ivec4",
            ShaderValueType.UnsignedIntVec2 => "uvec2",
            ShaderValueType.UnsignedIntVec3 => "uvec3",
            ShaderValueType.UnsignedIntVec4 => "uvec4",
            ShaderValueType.Bool => "bool",
            ShaderValueType.BoolVec2 => "bvec2",
            ShaderValueType.BoolVec3 => "bvec3",
            ShaderValueType.BoolVec4 => "bvec4",
            ShaderValueType.FloatMat2 => "mat2",
            ShaderValueType.FloatMat3 => "mat3",
            ShaderValueType.FloatMat4 => "mat4",
            ShaderValueType.Sampler1D => "sampler1D",
            ShaderValueType.Sampler2D => "sampler2D",
            ShaderValueType.Sampler3D => "sampler3D",
            ShaderValueType.SamplerCube => "samplerCube",
            ShaderValueType.Sampler1DArray => "sampler1DArray",
            ShaderValueType.Sampler2DArray => "sampler2DArray",
            ShaderValueType.SamplerCubeArray => "samplerCubeArray",
            ShaderValueType.SamplerBuffer => "samplerBuffer",
            ShaderValueType.IntSampler1D => "isampler1D",
            ShaderValueType.IntSampler2D => "isampler2D",
            ShaderValueType.IntSampler3D => "isampler3D",
            ShaderValueType.IntSamplerCube => "isamplerCube",
            ShaderValueType.IntSampler1DArray => "isampler1DArray",
            ShaderValueType.IntSampler2DArray => "isampler2DArray",
            ShaderValueType.IntSamplerBuffer => "isamplerBuffer",
            ShaderValueType.UnsignedIntSampler1D => "usampler1D",
            ShaderValueType.UnsignedIntSampler2D => "usampler2D",
            ShaderValueType.UnsignedIntSampler3D => "usampler3D",
            ShaderValueType.UnsignedIntSamplerCube => "usamplerCube",
            ShaderValueType.UnsignedIntSampler1DArray => "usampler1DArray",
            ShaderValueType.UnsignedIntSampler2DArray => "usampler2DArray",
            ShaderValueType.UnsignedIntSamplerBuffer => "usamplerBuffer",
            _ => ""
        };

    public static bool IsFlatType(this ShaderValueType type)
        => type is ShaderValueType.Int or ShaderValueType.UnsignedInt or ShaderValueType.Bool
            or ShaderValueType.IntVec2 or ShaderValueType.IntVec3 or ShaderValueType.IntVec4
            or ShaderValueType.UnsignedIntVec2 or ShaderValueType.UnsignedIntVec3
            or ShaderValueType.UnsignedIntVec4 or ShaderValueType.BoolVec2 or ShaderValueType.BoolVec3
            or ShaderValueType.BoolVec4;
}
