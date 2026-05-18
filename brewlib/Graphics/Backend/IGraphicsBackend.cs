namespace BrewLib.Graphics.Backend;

using System;
using System.Numerics;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using BrewLib.IO;

public interface IGraphicsBackend : IDisposable
{
    string Name { get; }
    GraphicsBackendCapabilities Capabilities { get; }
    ShaderSourceLanguage ShaderSourceLanguage { get; }

    IGraphicsDevice Device { get; }
    IRendererFactory RendererFactory { get; }
    IGraphicsBufferFactory Buffers { get; }
    ITransientGraphicsBufferFactory TransientBuffers { get; }
    IRenderPipelineFactory RenderPipelines { get; }
    IShaderAssetLoader ShaderAssets { get; }
    IShaderProgramFactory ShaderPrograms { get; }
    ITextureFactory TextureFactory { get; }
    IAsyncTextureUploader TextureUploader { get; }

    bool SupportsShaderExtension(string extensionName);
    void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer);
    bool BeginFrame(Vector4 clearColor);
    void EndFrame(bool discardFramebuffer);
}

public interface IRendererFactory
{
    IQuadRenderer CreateQuadRenderer();
    ILineRenderer CreateLineRenderer();
}
