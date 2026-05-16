namespace BrewLib.Graphics.Backend;

using System;
using System.IO;
using System.Reflection;
using BrewLib.Graphics.Shaders;

public sealed class EmbeddedShaderAssetLoader(Assembly assembly = null) : IShaderAssetLoader
{
    const string Root = "BrewLib.Graphics.Shaders.Compiled.";
    readonly Assembly assembly = assembly ?? typeof(EmbeddedShaderAssetLoader).Assembly;

    public bool TryLoad(
        string name,
        CompiledShaderStage stage,
        CompiledShaderFormat preferredFormat,
        out ShaderAsset shader)
    {
        var resourceName = $"{Root}{name}.{stage.ToString().ToLowerInvariant()}.{extensionFor(preferredFormat)}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            shader = default;
            return false;
        }

        using MemoryStream buffer = new((int)stream.Length);
        stream.CopyTo(buffer);
        shader = new(name, stage, preferredFormat, buffer.ToArray());
        return true;
    }

    static string extensionFor(CompiledShaderFormat format)
        => format switch
        {
            CompiledShaderFormat.Glsl => "glsl",
            CompiledShaderFormat.SpirV => "spv",
            CompiledShaderFormat.Dxil => "dxil",
            CompiledShaderFormat.Msl => "msl",
            CompiledShaderFormat.MetalLib => "metallib",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };
}
