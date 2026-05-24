namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using IO;
using Renderers;
using SDL3;
using Shaders;
using Silk.NET.OpenGL;
using Textures;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Util;
using ZLinq;

public sealed class OpenGlGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    SearchValues<string> extensions;
    Version glVersion;
    bool isOpenGlEs = true;

    public OpenGlGraphicsBackend()
    {
        var device = new OpenGlGraphicsDevice();
        Device = device;
        Buffers = new OpenGlGraphicsBufferFactory();
        TransientBuffers = new OpenGlTransientGraphicsBufferFactory(this);
        RenderPipelines = new OpenGlRenderPipelineFactory(this, device);
        ShaderPrograms = new OpenGlShaderProgramFactory(this, device);
        TextureFactory = new OpenGlTextureFactory(this);
        TextureUploader = new OpenGlAsyncTextureUploader(this);
    }

    internal uint GlslVersion { get; private set; } = 300;
    internal bool GlslEs { get; private set; } = true;

    public string Name => "OpenGL ES";
    public GraphicsBackendCapabilities Capabilities { get; private set; }
    public ShaderSourceLanguage ShaderSourceLanguage => ShaderSourceLanguage.Hlsl;
    public IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public IGraphicsBufferFactory Buffers { get; }
    public ITransientGraphicsBufferFactory TransientBuffers { get; }
    public IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; } = new EmbeddedShaderAssetLoader();
    public IShaderProgramFactory ShaderPrograms { get; }
    public ITextureFactory TextureFactory { get; }
    public IAsyncTextureUploader TextureUploader { get; }

    public bool SupportsShaderExtension(string extensionName)
        => extensions is not null && extensions.Contains(extensionName);

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        OpenGlApi.Load();
        initializeContext();
        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public bool BeginFrame(Vector4 clearColor)
    {
        Span<float> color = [clearColor.X, clearColor.Y, clearColor.Z, clearColor.W];
        OpenGlApi.GL.ClearBuffer(BufferKind.Color, 0, ref color.GetPinnableReference());
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        if (!discardFramebuffer) return;

        Span<InvalidateFramebufferAttachment> attachments = [InvalidateFramebufferAttachment.Color];
        OpenGlApi.GL.InvalidateFramebuffer(FramebufferTarget.Framebuffer,
            (uint)attachments.Length,
            ref attachments.GetPinnableReference());
    }

    public void Dispose()
    {
        Device.Dispose();
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

        return hasExtensions || HasVersion(major, minor);
    }

    bool HasVersion(int major, int minor)
        => glVersion is not null &&
            (glVersion.Major > major || glVersion.Major == major && glVersion.Minor >= minor);

    internal IGpuUploadFence CreateUploadFence()
        => new OpenGlUploadFence(OpenGlApi.GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, (uint)0));

    void initializeContext()
    {
        extensions = getExtensions();
        retrieveRendererInfo();
        initializeDebugCallback();

        var features = GraphicsBackendFeatures.TextureAtlases;

        if (isOpenGlEs ? HasVersion(3, 0) : HasCapabilities(3, 0, "GL_ARB_vertex_array_object"))
            features |= GraphicsBackendFeatures.VertexArrays;

        if (isOpenGlEs ? HasVersion(3, 0) : HasCapabilities(3, 3, "GL_ARB_instanced_arrays"))
            features |= GraphicsBackendFeatures.Instancing;

        if (isOpenGlEs ?
            HasVersion(3, 1) && SupportsShaderExtension("GL_EXT_multi_draw_indirect") :
            HasCapabilities(4, 3, "GL_ARB_multi_draw_indirect"))
            features |= GraphicsBackendFeatures.IndirectDraws;

        if (isOpenGlEs ? HasVersion(3, 1) : HasCapabilities(4, 3, "GL_ARB_compute_shader"))
            features |= GraphicsBackendFeatures.ComputeShaders;

        if (!isOpenGlEs && HasCapabilities(4, 3, "GL_ARB_invalidate_subdata"))
            features |= GraphicsBackendFeatures.FramebufferInvalidation;

        if (!isOpenGlEs && HasCapabilities(4, 4, "GL_ARB_buffer_storage") ||
            isOpenGlEs && SupportsShaderExtension("GL_EXT_buffer_storage"))
            features |= GraphicsBackendFeatures.ImmutableBuffers;

        if (!isOpenGlEs && HasCapabilities(4, 4, "GL_ARB_clear_texture"))
            features |= GraphicsBackendFeatures.ClearTexture;

        if (tryEnableSrgbFramebuffer())
            features |= GraphicsBackendFeatures.SrgbFramebuffer;
        else if (shouldUseManualColorCorrection())
            features |= GraphicsBackendFeatures.ManualColorCorrection;

        var maxTextureImageUnits = OpenGlApi.GL.GetInteger(GetPName.MaxTextureImageUnits);
        var maxVertexTextureImageUnits = OpenGlApi.GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
        var maxGeometryTextureImageUnits = SupportsShaderExtension("GL_ARB_geometry_shader4") ?
            OpenGlApi.GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) :
            0;

        var maxCombinedTextureImageUnits = OpenGlApi.GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        var maxTextureSize = OpenGlApi.GL.GetInteger(GetPName.MaxTextureSize);
        var maxUniformBufferSize = OpenGlApi.GL.GetInteger(GetPName.MaxUniformBlockSize);

        if (!isOpenGlEs)
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
        using var extensionNames = ValueEnumerable.Range(0, OpenGlApi.GL.GetInteger(GetPName.NumExtensions))
            .Select(i => OpenGlApi.GL.GetStringS(StringName.Extensions, (uint)i))
            .ToArrayPool();

        return SearchValues.Create(extensionNames.Span, StringComparison.OrdinalIgnoreCase);
    }

    unsafe void initializeDebugCallback()
    {
        if (isOpenGlEs ?
            !SupportsShaderExtension("GL_KHR_debug") :
            !HasCapabilities(4, 3, "GL_KHR_debug"))
            return;

        OpenGlApi.GL.Enable(EnableCap.DebugOutputSynchronous);
        OpenGlApi.GL.DebugMessageCallback((source, type, _, severity, length, message, _) =>
            {
                var bytes = message.AsReadOnlySpan<byte>(length);
                var debugSource = (DebugSource)source;
                var debugType = (DebugType)type;
                var debugSeverity = (DebugSeverity)severity;

                Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(bytes)];
                Encoding.UTF8.GetChars(bytes, chars);

                using var str = StringHelper.Interpolate(CultureInfo.InvariantCulture,
                    $"{chars} (Source: {debugSource switch
                    {
                        DebugSource.DebugSourceApi => "API",
                        DebugSource.DebugSourceWindowSystem => "Window System",
                        DebugSource.DebugSourceShaderCompiler => "Shader Compiler",
                        DebugSource.DebugSourceThirdParty => "Third Party",
                        DebugSource.DebugSourceApplication => "Application",
                        DebugSource.DebugSourceOther => "Other",
                        _ => ""
                    }}, Type: {debugType switch
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
                }}, Severity: {debugSeverity switch
            {
                DebugSeverity.DebugSeverityHigh => "High",
                DebugSeverity.DebugSeverityMedium => "Medium",
                DebugSeverity.DebugSeverityLow => "Low",
                DebugSeverity.DebugSeverityNotification => "Notification",
                _ => ""
            }})\n");

                switch (debugSeverity)
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
            null);
    }

    bool tryEnableSrgbFramebuffer()
    {
        if (!DrawState.UseSrgb) return false;

        if (!tryGetDefaultFramebufferColorEncoding(out var defaultFramebufferColorEncoding) ||
            defaultFramebufferColorEncoding != (int)GLEnum.Srgb)
        {
            SDL.LogWarn(LogCategory.Render, "The default framebuffer isn't sRgb");
            return false;
        }

        if (isOpenGlEs && !SupportsShaderExtension("GL_EXT_sRGB_write_control"))
        {
            SDL.LogWarn(LogCategory.Render,
                "OpenGL ES sRGB framebuffer write control is unavailable; using shader-side output correction");

            return false;
        }

        Device.SetCapability(GraphicsCapability.FramebufferSrgb, true);
        return true;
    }

    bool shouldUseManualColorCorrection()
    {
        if (!DrawState.UseSrgb || !isOpenGlEs || SupportsShaderExtension("GL_EXT_sRGB_write_control"))
            return false;

        if (!tryGetDefaultFramebufferColorEncoding(out var defaultFramebufferColorEncoding) ||
            defaultFramebufferColorEncoding != (int)GLEnum.Srgb)
            return false;

        SDL.LogInfo(LogCategory.Render, "OpenGL ES default framebuffer is sRGB; enabling shader-side output correction");
        return true;
    }

    bool tryGetDefaultFramebufferColorEncoding(out int colorEncoding)
    {
        try
        {
            OpenGlApi.GL.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer,
                isOpenGlEs ? GLEnum.Back : GLEnum.BackLeft,
                FramebufferAttachmentParameterName.ColorEncoding,
                out colorEncoding);

            return true;
        }
        catch (Exception e)
        {
            SDL.LogWarn(LogCategory.Render, $"Unable to query default framebuffer color encoding: {e.Message}");
            colorEncoding = 0;
            return false;
        }
    }

    void logTextureFormatPreferences()
    {
        OpenGlApi.GL.GetInternalformat(TextureTarget.Texture2D,
            InternalFormat.Rgba8,
            InternalFormatPName.TextureImageFormat,
            1,
            out long preferredFormat);

        SDL.LogInfo(LogCategory.Render, $"preferred texture format: {Enum.GetName((PixelFormat)(int)preferredFormat)}");

        OpenGlApi.GL.GetInternalformat(TextureTarget.Texture2D,
            InternalFormat.Rgba8,
            InternalFormatPName.TextureImageType,
            1,
            out preferredFormat);

        SDL.LogInfo(LogCategory.Render, $"preferred texture type: 0x{preferredFormat:x}");
    }

    void retrieveRendererInfo()
    {
        var glVersionString = OpenGlApi.GL.GetStringS(StringName.Version);
        glVersion = parseVersion(glVersionString);
        isOpenGlEs = glVersionString.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase) || GlslEs;
        GlslEs = isOpenGlEs;
        GlslVersion = isOpenGlEs ? 300u : 150u;
        SDL.LogInfo(LogCategory.Render, $"{(isOpenGlEs ? "OpenGL ES" : "OpenGL")} v{glVersionString}");

        var rendererName = OpenGlApi.GL.GetStringS(StringName.Renderer);
        var rendererVendor = OpenGlApi.GL.GetStringS(StringName.Vendor);
        SDL.LogInfo(LogCategory.Render, $"Renderer: {rendererName} | Vendor: {rendererVendor}");

        var minimumVersion = isOpenGlEs ? new(3, 0) : new Version(3, 2);
        if (glVersion < minimumVersion)
            throw new NotSupportedException(
                $"This application requires at least {(isOpenGlEs ? "OpenGL ES 3.0" : "OpenGL 3.2")} " +
                $"(version {glVersion} found)\n{rendererName} ({rendererVendor})");

        SDL.LogInfo(LogCategory.Render, $"GLSL v{OpenGlApi.GL.GetStringS(StringName.ShadingLanguageVersion)}");
    }

    static Version parseVersion(string versionString)
    {
        var start = -1;
        for (var i = 0; i < versionString.Length; ++i)
            if (char.IsAsciiDigit(versionString[i]))
            {
                start = i;
                break;
            }

        if (start < 0)
            throw new InvalidOperationException($"Unable to parse OpenGL version string '{versionString}'");

        var end = start;
        while (end < versionString.Length &&
            (char.IsAsciiDigit(versionString[end]) || versionString[end] == '.'))
            ++end;

        return Version.TryParse(versionString.AsSpan(start, end - start), out var version)
            ? version
            : throw new InvalidOperationException($"Unable to parse OpenGL version string '{versionString}'");
    }
}