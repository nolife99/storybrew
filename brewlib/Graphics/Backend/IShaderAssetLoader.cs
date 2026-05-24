namespace BrewLib.Graphics.Backend;

using Shaders;

public interface IShaderAssetLoader
{
    bool TryLoad(
        string name,
        CompiledShaderStage stage,
        CompiledShaderFormat preferredFormat,
        out ShaderAsset shader);
}