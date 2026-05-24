namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using Textures;

public sealed class OpenGlResourceSet : IResourceSet
{
    readonly OpenGlGraphicsDevice device;
    readonly OpenGlRenderPipeline pipeline;
    readonly TextureBinding[] textureBindings;

    bool disposed;

    public OpenGlResourceSet(OpenGlGraphicsDevice device, OpenGlRenderPipeline pipeline)
    {
        this.device = device;
        this.pipeline = pipeline;

        var pipelineTextureBindings = pipeline.TextureBindings;
        textureBindings = new TextureBinding[pipelineTextureBindings.Length];
        for (var i = 0; i < pipelineTextureBindings.Length; ++i)
        {
            var binding = pipelineTextureBindings[i];
            textureBindings[i] = new(binding.Binding,
                binding.Name,
                binding.Capacity,
                binding.Location);
        }
    }

    public void SetTextures(int binding, scoped ReadOnlySpan<ITexture> textures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ref var textureBinding = ref getTextureBinding(binding);
        if (textures.Length > textureBinding.Textures.Length)
            throw new ArgumentException(
                $"Texture binding {binding} accepts at most {textureBinding.Textures.Length} textures",
                nameof(textures));

        textures.CopyTo(textureBinding.Textures);
        if (textureBinding.Count > textures.Length)
            Array.Clear(textureBinding.Textures, textures.Length, textureBinding.Count - textures.Length);

        textureBinding.Count = textures.Length;
    }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        pipeline.Bind();
        foreach (var textureBinding in textureBindings)
        {
            if (textureBinding.Count == 0) continue;

            var textures = textureBinding.Textures.AsSpan(0, textureBinding.Count);
            device.BindTextures(textures, textureBinding.TextureUnits);
            OpenGlApi.GL.Uniform1(textureBinding.Location, textureBinding.TextureUnits.AsSpan(0, textureBinding.Count));
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var textureBinding in textureBindings)
            Array.Clear(textureBinding.Textures, 0, textureBinding.Count);

        disposed = true;
    }

    ref TextureBinding getTextureBinding(int binding)
    {
        for (var i = 0; i < textureBindings.Length; ++i)
            if (textureBindings[i].Binding == binding)
                return ref textureBindings[i];

        throw new ArgumentException($"Texture binding {binding} is not part of this resource set", nameof(binding));
    }

    sealed class TextureBinding
    {
        public TextureBinding(int binding, string name, int capacity, int location)
        {
            Binding = binding;
            Name = name;
            Location = location;
            Textures = new ITexture[capacity];
            TextureUnits = new int[capacity];
        }

        public int Binding { get; }
        public string Name { get; }
        public int Location { get; }
        public ITexture[] Textures { get; }
        public int[] TextureUnits { get; }
        public int Count { get; set; }
    }
}