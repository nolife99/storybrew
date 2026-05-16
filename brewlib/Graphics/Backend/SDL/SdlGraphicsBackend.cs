namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Numerics;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using BrewLib.IO;
using SDL3;

public sealed class SdlGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    readonly SdlShaderCrossContext shaderCrossContext;
    readonly nint window;
    nint commandBuffer, renderPass, swapchainTexture;
    uint swapchainWidth, swapchainHeight;
    bool disposed;

    public SdlGraphicsBackend(nint window = 0,
        bool debug = false,
        string preferredDriver = null)
    {
        shaderCrossContext = new();
        this.window = window;

        var requestedFormats = SdlShaderCrossContext.SupportedHlslShaderFormats |
            SdlShaderCrossContext.SupportedSpirVShaderFormats |
            SDL.GPUShaderFormat.SPIRV;

        var deviceHandle = SDL.CreateGPUDevice(requestedFormats, debug, preferredDriver);
        if (deviceHandle == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU device: {SDL.GetError()}");

        var device = new SdlGraphicsDevice(this, deviceHandle);
        Device = device;
        Buffers = new SdlGraphicsBufferFactory(this);
        RenderPipelines = new SdlRenderPipelineFactory(this);
        ShaderPrograms = new SdlShaderProgramFactory();
        TextureFactory = new SdlTextureFactory(this);

        var driver = SDL.GetGPUDeviceDriver(deviceHandle) ?? "unknown";
        var shaderFormats = SDL.GetGPUShaderFormats(deviceHandle);
        SDL.LogInfo(LogCategory.Render, $"SDL GPU driver: {driver}; shader formats: {shaderFormats}");

        Capabilities = new(GraphicsBackendFeatures.TextureAtlases | GraphicsBackendFeatures.Instancing,
            16384,
            16,
            0,
            0,
            16,
            0);
    }

    public string Name => "SDL GPU";
    public GraphicsBackendCapabilities Capabilities { get; }
    public ShaderSourceLanguage ShaderSourceLanguage => BrewLib.Graphics.Shaders.ShaderSourceLanguage.Hlsl;
    public IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public IGraphicsBufferFactory Buffers { get; }
    public IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; } = new EmbeddedShaderAssetLoader();
    public IShaderProgramFactory ShaderPrograms { get; }
    public ITextureFactory TextureFactory { get; }

    internal nint DeviceHandle => ((SdlGraphicsDevice)Device).DeviceHandle;
    internal nint Window => window;
    internal nint CommandBuffer => commandBuffer;
    internal nint RenderPass => renderPass;
    internal SDL.GPUTextureFormat SwapchainFormat { get; private set; }
    internal uint SwapchainHeight => swapchainHeight;

    public bool SupportsShaderExtension(string extensionName) => false;

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        if (window != nint.Zero && !SDL.ClaimWindowForGPUDevice(DeviceHandle, window))
            throw new InvalidOperationException($"Unable to claim SDL window for GPU device: {SDL.GetError()}");

        if (window != nint.Zero)
            SwapchainFormat = SDL.GetGPUSwapchainTextureFormat(DeviceHandle, window);

        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public IQuadRenderer CreateQuadRenderer()
        => new TexturedQuadRenderer(this);

    public ILineRenderer CreateLineRenderer()
        => new LineRenderer(this);

    public bool BeginFrame(Vector4 clearColor)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (window == nint.Zero)
            throw new InvalidOperationException("SDL frame rendering requires a claimed SDL window");
        if (commandBuffer != nint.Zero)
            throw new InvalidOperationException("An SDL GPU frame is already active");

        commandBuffer = SDL.AcquireGPUCommandBuffer(DeviceHandle);
        if (commandBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to acquire SDL GPU command buffer: {SDL.GetError()}");

        if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer,
                window,
                out swapchainTexture,
                out swapchainWidth,
                out swapchainHeight))
            throw new InvalidOperationException($"Unable to acquire SDL GPU swapchain texture: {SDL.GetError()}");

        if (swapchainTexture == nint.Zero)
        {
            submitCommandBuffer();
            return false;
        }

        SDL.GPUColorTargetInfo[] colorTargets =
        [
            new()
            {
                Texture = swapchainTexture,
                ClearColor = new() { R = clearColor.X, G = clearColor.Y, B = clearColor.Z, A = clearColor.W },
                LoadOp = SDL.GPULoadOp.Clear,
                StoreOp = SDL.GPUStoreOp.Store,
                Cycle = 0
            }
        ];

        renderPass = SDL.BeginGPURenderPass(commandBuffer, in colorTargets, 1, 0);
        if (renderPass == nint.Zero)
            throw new InvalidOperationException($"Unable to begin SDL GPU render pass: {SDL.GetError()}");

        ((SdlGraphicsDevice)Device).ApplyRenderPassState(renderPass);
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (commandBuffer == nint.Zero) return;

        if (renderPass != nint.Zero)
        {
            SDL.EndGPURenderPass(renderPass);
            renderPass = nint.Zero;
        }

        submitCommandBuffer();
        swapchainTexture = nint.Zero;
        swapchainWidth = swapchainHeight = 0;
    }

    public void Dispose()
    {
        if (disposed) return;

        if (window != nint.Zero) SDL.ReleaseWindowFromGPUDevice(DeviceHandle, window);
        Device.Dispose();
        shaderCrossContext.Dispose();

        disposed = true;
    }

    void submitCommandBuffer()
    {
        if (commandBuffer == nint.Zero) return;

        var submitted = SDL.SubmitGPUCommandBuffer(commandBuffer);
        commandBuffer = nint.Zero;

        if (!submitted)
            throw new InvalidOperationException($"Unable to submit SDL GPU command buffer: {SDL.GetError()}");
    }
}
