namespace BrewLib.Graphics.Backend;

using System;
using IO;
using Renderers;
using Shaders;
using Textures;

/// <summary>
/// Small common base for modern backends.
///
/// A backend is responsible for device/swapchain/resource ownership. Renderers should only ask for
/// buffers, pipelines, resource sets and draw calls; they should not know how a frame is submitted.
/// </summary>
public abstract class GraphicsBackendBase : IGraphicsBackend, IRendererFactory
{
    protected GraphicsBackendBase()
    {
        ShaderAssets = new EmbeddedShaderAssetLoader();
    }

    public abstract string Name { get; }
    public abstract GraphicsBackendCapabilities Capabilities { get; protected set; }
    public abstract ShaderSourceLanguage ShaderSourceLanguage { get; }

    public abstract IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public abstract IGraphicsBufferFactory Buffers { get; }
    public abstract IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; }
    public virtual IShaderProgramFactory ShaderPrograms => throw new NotSupportedException($"{Name} uses render-pipeline objects instead of legacy shader programs.");
    public abstract ITextureFactory TextureFactory { get; }
    public abstract IAsyncTextureUploader TextureUploader { get; }

    public virtual bool SupportsShaderExtension(string extensionName) => false;

    public abstract void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer);
    public abstract bool BeginFrame(System.Numerics.Vector4 clearColor);
    public abstract void EndFrame(bool discardFramebuffer);
    public abstract IQuadRenderer CreateQuadRenderer();
    public abstract ILineRenderer CreateLineRenderer();
    public abstract void Dispose();
}
