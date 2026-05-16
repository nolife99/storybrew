namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using BrewLib.IO;
using BrewLib.Util;
using osuTK.Graphics.OpenGL;
using SDL3;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using ZLinq;

public sealed class OpenGlGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    SearchValues<string> extensions;
    Version glVersion;

    public string Name => "OpenGL";
    public GraphicsBackendCapabilities Capabilities { get; private set; }
    public ShaderSourceLanguage ShaderSourceLanguage => BrewLib.Graphics.Shaders.ShaderSourceLanguage.Glsl;
    public IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public IGraphicsBufferFactory Buffers { get; }
    public IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; } = new EmbeddedShaderAssetLoader();
    public IShaderProgramFactory ShaderPrograms { get; }
    public ITextureFactory TextureFactory { get; }

    public OpenGlGraphicsBackend()
    {
        var device = new OpenGlGraphicsDevice();
        Device = device;
        Buffers = new OpenGlGraphicsBufferFactory();
        RenderPipelines = new OpenGlRenderPipelineFactory(this, device);
        ShaderPrograms = new OpenGlShaderProgramFactory(device);
        TextureFactory = new OpenGlTextureFactory(this);
    }

    public bool SupportsShaderExtension(string extensionName)
        => extensions is not null && extensions.Contains(extensionName);

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        initializeContext();
        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public bool BeginFrame(Vector4 clearColor)
    {
        Span<float> color = [clearColor.X, clearColor.Y, clearColor.Z, clearColor.W];
        GL.ClearBuffer(ClearBuffer.Color, 0, ref color.GetPinnableReference());
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        if (!discardFramebuffer) return;

        Span<FramebufferAttachment> attachments = [FramebufferAttachment.Color];
        GL.InvalidateFramebuffer(FramebufferTarget.Framebuffer,
            attachments.Length,
            ref attachments.GetPinnableReference());
    }

    public IQuadRenderer CreateQuadRenderer()
    {
        if (!Capabilities.Has(GraphicsBackendFeatures.Instancing))
            throw new NotSupportedException("The OpenGL quad renderer requires GPU instancing support");

        return new TexturedQuadRenderer(this);
    }

    public ILineRenderer CreateLineRenderer() => new LineRenderer(this);

    public bool HasCapabilities(int major, int minor, params ReadOnlySpan<string> requiredExtensions)
    {
        var hasExtensions = requiredExtensions.Length > 0 &&
            extensions is not null &&
            requiredExtensions.AsValueEnumerable().All(extensions.Contains);

        return hasExtensions ||
            glVersion is not null &&
            (glVersion.Major > major || glVersion.Major == major && glVersion.Minor >= minor);
    }

    public void Dispose()
    {
        Device.Dispose();
    }

    void initializeContext()
    {
        extensions = getExtensions();
        retrieveRendererInfo();
        initializeDebugCallback();

        var features = GraphicsBackendFeatures.TextureAtlases;

        if (HasCapabilities(3, 0, "GL_ARB_vertex_array_object"))
            features |= GraphicsBackendFeatures.VertexArrays;

        if (HasCapabilities(3, 3, "GL_ARB_instanced_arrays"))
            features |= GraphicsBackendFeatures.Instancing;

        if (HasCapabilities(4, 0, "GL_ARB_draw_indirect"))
            features |= GraphicsBackendFeatures.IndirectDraws;

        if (HasCapabilities(4, 3, "GL_ARB_compute_shader"))
            features |= GraphicsBackendFeatures.ComputeShaders;

        if (HasCapabilities(4, 3, "GL_ARB_invalidate_subdata"))
            features |= GraphicsBackendFeatures.FramebufferInvalidation;

        if (HasCapabilities(4, 4, "GL_ARB_buffer_storage"))
            features |= GraphicsBackendFeatures.ImmutableBuffers;

        if (HasCapabilities(4, 4, "GL_ARB_clear_texture"))
            features |= GraphicsBackendFeatures.ClearTexture;

        if (tryEnableSrgbFramebuffer())
            features |= GraphicsBackendFeatures.SrgbFramebuffer;

        DrawState.UseTextureCompression &= SupportsShaderExtension("GL_EXT_texture_compression_s3tc");
        if (DrawState.UseTextureCompression)
            features |= GraphicsBackendFeatures.TextureCompression;

        var maxTextureImageUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);
        var maxVertexTextureImageUnits = GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
        var maxGeometryTextureImageUnits = SupportsShaderExtension("GL_ARB_geometry_shader4") ?
            GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) :
            0;

        var maxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        var maxTextureSize = GL.GetInteger(GetPName.MaxTextureSize);
        var maxUniformBufferSize = GL.GetInteger(GetPName.MaxUniformBlockSize);

        logTextureFormatPreferences();

        SDL.LogInfo(LogCategory.Render,
            $"texture units available: ps:{maxTextureImageUnits} vs:{maxVertexTextureImageUnits} gs:{maxGeometryTextureImageUnits} combined:{maxCombinedTextureImageUnits}");

        SDL.LogInfo(LogCategory.Render, $"max texture size: {maxTextureSize}");
        SDL.LogInfo(LogCategory.Render, $"max uniform buffer size: {maxUniformBufferSize}");

        Device.InitializeTextureSlots(maxTextureImageUnits);

        Capabilities = new(features,
            maxTextureSize,
            maxTextureImageUnits,
            maxVertexTextureImageUnits,
            maxGeometryTextureImageUnits,
            maxCombinedTextureImageUnits,
            maxUniformBufferSize);
    }

    SearchValues<string> getExtensions()
    {
        using var extensionNames = ValueEnumerable.Range(0, GL.GetInteger(GetPName.NumExtensions))
            .Select(i => GL.GetString(StringNameIndexed.Extensions, i))
            .ToArrayPool();

        return SearchValues.Create(extensionNames.Span, StringComparison.OrdinalIgnoreCase);
    }

    void initializeDebugCallback()
    {
        if (!HasCapabilities(4, 3, "GL_KHR_debug")) return;

        GL.Enable(EnableCap.DebugOutputSynchronous);
        GL.DebugMessageCallback((source, type, _, severity, length, message, _) =>
            {
                var bytes = message.AsReadOnlySpan<byte>(length);

                Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(bytes)];
                Encoding.UTF8.GetChars(bytes, chars);

                using var str = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                    $"{chars} (Source: {source switch
                    {
                        DebugSource.DebugSourceApi => "API",
                        DebugSource.DebugSourceWindowSystem => "Window System",
                        DebugSource.DebugSourceShaderCompiler => "Shader Compiler",
                        DebugSource.DebugSourceThirdParty => "Third Party",
                        DebugSource.DebugSourceApplication => "Application",
                        DebugSource.DebugSourceOther => "Other",
                        _ => ""
                    }}, Type: {type switch
                    {
                        DebugType.DebugTypeError => "Error",
                        DebugType.DebugTypeDeprecatedBehavior => "Deprecated Behaviour",
                        DebugType.DebugTypeUndefinedBehavior => "Undefined Behaviour",
                        DebugType.DebugTypePortability => "Portability",
                        DebugType.DebugTypePerformance => "Performance",
                        DebugType.DebugTypeMarker => "Marker",
                        DebugType.DebugTypePushGroup => "Push Group",
                        DebugType.DebugTypePopGroup => "Pop Group",
                        DebugType.DebugTypeOther => "Other",
                        _ => ""
                    }}, Severity: {severity switch
                    {
                        DebugSeverity.DebugSeverityHigh => "High",
                        DebugSeverity.DebugSeverityMedium => "Medium",
                        DebugSeverity.DebugSeverityLow => "Low",
                        DebugSeverity.DebugSeverityNotification => "Notification",
                        _ => ""
                    }})\n");

                switch (severity)
                {
                    case DebugSeverity.DebugSeverityHigh:
                        SDL.LogError(LogCategory.Render, str.AsReadOnlySpan());
                        throw new InvalidDataException($"OpenGL error: {str.AsReadOnlySpan()}");

                    case DebugSeverity.DebugSeverityMedium:
                        SDL.LogWarn(LogCategory.Render, str.AsReadOnlySpan());
                        break;

                    case DebugSeverity.DebugSeverityLow:
                        SDL.LogInfo(LogCategory.Render, str.AsReadOnlySpan());
                        break;

                    case DebugSeverity.DebugSeverityNotification:
                        SDL.LogDebug(LogCategory.Render, str.AsReadOnlySpan());
                        break;
                }
            },
            0);
    }

    bool tryEnableSrgbFramebuffer()
    {
        if (!DrawState.UseSrgb) return false;

        GL.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer,
            FramebufferAttachment.BackLeft,
            FramebufferParameterName.FramebufferAttachmentColorEncoding,
            out var defaultFramebufferColorEncoding);

        if (defaultFramebufferColorEncoding != 0x8C40)
        {
            SDL.LogWarn(LogCategory.Render, "The default framebuffer isn't sRgb");
            return false;
        }

        Device.SetCapability(GraphicsCapability.FramebufferSrgb, true);
        return true;
    }

    void logTextureFormatPreferences()
    {
        GL.GetInternalformat(ImageTarget.Texture2D,
            SizedInternalFormat.Rgba8,
            InternalFormatParameter.TextureImageFormat,
            1,
            out int preferredFormat);

        SDL.LogInfo(LogCategory.Render, $"preferred texture format: {Enum.GetName((PixelFormat)preferredFormat)}");

        GL.GetInternalformat(ImageTarget.Texture2D,
            SizedInternalFormat.Rgba8,
            InternalFormatParameter.TextureImageType,
            1,
            out preferredFormat);

        SDL.LogInfo(LogCategory.Render, $"preferred texture type: 0x{preferredFormat:x}");
    }

    void retrieveRendererInfo()
    {
        var glVersionString = GL.GetString(StringName.Version);
        glVersion = new(glVersionString.Split(' ')[0]);
        SDL.LogInfo(LogCategory.Render, $"OpenGL v{glVersionString}");

        var rendererName = GL.GetString(StringName.Renderer);
        var rendererVendor = GL.GetString(StringName.Vendor);
        SDL.LogInfo(LogCategory.Render, $"Renderer: {rendererName} | Vendor: {rendererVendor}");

        if (glVersion < new Version(3, 2))
            throw new NotSupportedException(
                $"This application requires at least OpenGL 3.2 (version {glVersion} found)\n{rendererName} ({rendererVendor})");

        SDL.LogInfo(LogCategory.Render, $"GLSL v{GL.GetString(StringName.ShadingLanguageVersion)}");
    }
}
