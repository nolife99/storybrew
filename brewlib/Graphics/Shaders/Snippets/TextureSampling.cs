namespace BrewLib.Graphics.Shaders.Snippets
{
    public class TextureSampling(ShaderVariable result, ShaderVariable sampler, ShaderVariable coord) : Assign(result, () => $"texture2D({sampler.Ref}, {coord.Ref})")
    {
    }
}