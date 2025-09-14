namespace BrewLib.Graphics;

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Text;
using BrewLib.Graphics.Textures;
using BrewLib.IO;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using ZLinq;

public static class DrawState
{
    public static readonly SearchValues<string> Extensions = getExtensions();
    public static readonly bool UseSrgb, SupportsImmutable = Extensions.Contains("GL_ARB_buffer_storage");

    static IRenderer renderer;

    static bool flushingRenderer;
    static int drawCalls, activeTextureUnit;
    public static bool UseTextureCompression { get; set; }

    public static bool CanInvalidate { get; private set; }
    public static bool ColorCorrected { get; private set; }
    public static int MaxTextureSize { get; private set; }

    public static int ActiveTextureUnit
    {
        get => activeTextureUnit;
        set
        {
            if (activeTextureUnit == value) return;

            GL.ActiveTexture(TextureUnit.Texture0 + value);
            activeTextureUnit = value;
        }
    }

    public static IRenderer Renderer
    {
        get => renderer;
        set
        {
            if (renderer == value) return;

            FlushRenderer(true);

            flushingRenderer = true;
            renderer?.EndRendering();

            renderer = value;

            renderer?.BeginRendering();
            flushingRenderer = false;
        }
    }

    static SearchValues<string> getExtensions()
    {
        using var extensions = ValueEnumerable.Range(0, GL.GetInteger(GetPName.NumExtensions))
            .Select(i => GL.GetString(StringNameIndexed.Extensions, i))
            .ToArrayPool();

        return SearchValues.Create(extensions.Span, StringComparison.OrdinalIgnoreCase);
    }

    public static void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        if (Extensions.Contains("GL_KHR_debug"))
        {
            GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.Khr.DebugMessageCallback((source, type, _, severity, length, message, _) =>
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
                            SDL.LogError(SDL.LogCategory.Render, str.AsReadOnlySpan());
                            throw new InvalidDataException($"OpenGL error: {str.AsReadOnlySpan()}");

                        case DebugSeverity.DebugSeverityMedium:
                            SDL.LogWarn(SDL.LogCategory.Render, str.AsReadOnlySpan());
                            break;

                        case DebugSeverity.DebugSeverityLow:
                            SDL.LogInfo(SDL.LogCategory.Render, str.AsReadOnlySpan());
                            break;

                        case DebugSeverity.DebugSeverityNotification:
                            SDL.LogDebug(SDL.LogCategory.Render, str.AsReadOnlySpan());
                            break;
                    }
                },
                0);
        }

        retrieveRendererInfo();
        if (UseSrgb)
        {
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer,
                FramebufferAttachment.BackLeft,
                FramebufferParameterName.FramebufferAttachmentColorEncoding,
                out var defaultFramebufferColorEncoding);

            if (defaultFramebufferColorEncoding == 0x8C40)
            {
                SetCapability(EnableCap.FramebufferSrgb, true);
                ColorCorrected = true;
            }
            else SDL.LogWarn(SDL.LogCategory.Render, "The default framebuffer isn't sRgb");
        }

        UseTextureCompression &= Extensions.Contains("GL_EXT_texture_compression_s3tc");
        CanInvalidate = Extensions.Contains("GL_ARB_invalidate_subdata");

        maxTextureImageUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);
        maxVertexTextureImageUnits = GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
        maxGeometryTextureImageUnits = Extensions.Contains("GL_ARB_geometry_shader4") ?
            GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) :
            0;

        maxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        MaxTextureSize = GL.GetInteger(GetPName.MaxTextureSize);

        GL.GetInternalformat(ImageTarget.Texture2D,
            SizedInternalFormat.Rgba8,
            InternalFormatParameter.TextureImageFormat,
            1,
            out int preferredFormat);

        SDL.LogInfo(SDL.LogCategory.Render, $"preferred texture format: {Enum.GetName((PixelFormat)preferredFormat)}");

        GL.GetInternalformat(ImageTarget.Texture2D,
            SizedInternalFormat.Rgba8,
            InternalFormatParameter.TextureImageType,
            1,
            out preferredFormat);

        SDL.LogInfo(SDL.LogCategory.Render, $"preferred texture type: {preferredFormat:x}");

        SDL.LogInfo(SDL.LogCategory.Render,
            $"texture units available: ps:{maxTextureImageUnits} vs:{maxVertexTextureImageUnits} gs:{maxGeometryTextureImageUnits} combined:{maxCombinedTextureImageUnits}");

        SDL.LogInfo(SDL.LogCategory.Render, $"max texture size: {MaxTextureSize}");
        SDL.LogInfo(SDL.LogCategory.Render, $"max uniform buffer size: {GL.GetInteger(GetPName.MaxUniformBlockSize)}");

        samplerTextureIds = new int[maxTextureImageUnits];
        samplerTexturingModes = new TextureTarget[maxTextureImageUnits];

        WhitePixel = Texture2d.Create(Color.White);
        TransparentPixel = Texture2d.Create(Color.Transparent);

        TextGenerator = new(resourceContainer);
        TextFontManager = new(textureContainer);
    }

    public static void Cleanup()
    {
        WhitePixel.Dispose();
        TransparentPixel.Dispose();
        TextFontManager.Dispose();
        TextGenerator.Dispose();
        capabilityCache.Dispose();
    }

    public static int CompleteFrame()
    {
        Renderer = null;

        var totalDraws = drawCalls;
        drawCalls = 0;

        capabilityCache.Clear();
        RenderStates.ClearStateCache();

        return totalDraws;
    }

    public static void FlushRenderer(bool canBuffer = false)
    {
        if (renderer is null || flushingRenderer) return;

        flushingRenderer = true;
        if (canBuffer) ++drawCalls;
        renderer.Flush(canBuffer);
        flushingRenderer = false;
    }

    public static T Prepare<T>(T nextRenderer, ICamera camera, RenderStates renderStates) where T : IRenderer
    {
        Renderer = nextRenderer;
        renderer.Camera = camera;
        renderStates.Apply();
        return nextRenderer;
    }

    #region Texture states

    public static Texture2d WhitePixel { get; private set; }
    public static Texture2d TransparentPixel { get; private set; }

    static int[] samplerTextureIds;
    static TextureTarget[] samplerTexturingModes;

    static int lastRecycledTextureUnit = -1, maxTextureImageUnits, maxVertexTextureImageUnits,
        maxGeometryTextureImageUnits, maxCombinedTextureImageUnits;

    static void SetTexturingMode(int samplerIndex, TextureTarget mode)
    {
        ref var previousMode = ref samplerTexturingModes[samplerIndex];
        if (previousMode == mode) return;

        if (samplerTextureIds[samplerIndex] != 0) UnbindTexture(samplerTextureIds[samplerIndex]);
        previousMode = mode;
    }

    public static void BindPrimaryTexture(int textureId,
        TextureTarget mode = TextureTarget.Texture2D,
        bool activate = false)
        => BindTexture(textureId, 0, mode, activate);

    static void BindTexture(int textureId, int samplerIndex, TextureTarget mode, bool activate)
    {
        if (activate) ActiveTextureUnit = samplerIndex;
        SetTexturingMode(samplerIndex, mode);

        ref var samplerTextureId = ref samplerTextureIds[samplerIndex];
        if (samplerTextureId == textureId) return;

        GL.BindTexture(mode, textureId);
        samplerTextureId = textureId;
    }

    public static int BindTexture(int textureId, bool activate = true) => BindTextures([textureId], activate);

    static int BindTextures(scoped ReadOnlySpan<int> textures, bool activate)
    {
        Span<int> samplerIndexes = stackalloc int[textures.Length];
        for (var i = 0; i < textures.Length; ++i)
        {
            var textureId = textures[i];

            samplerIndexes[i] = -1;
            for (var j = 0; j < samplerTextureIds.Length; ++j)
                if (samplerTextureIds[j] == textureId)
                {
                    samplerIndexes[i] = j;
                    break;
                }
        }

        var samplerCount = samplerTextureIds.Length;
        for (var i = 0; i < textures.Length; ++i)
        {
            if (samplerIndexes[i] != -1) continue;

            var first = true;
            var samplerStartIndex = (lastRecycledTextureUnit + 1) % samplerCount;
            for (var samplerIndex = samplerStartIndex; first || samplerIndex != samplerStartIndex;
                samplerIndex = (samplerIndex + 1) % samplerCount)
            {
                first = false;

                var isFreeSamplerUnit = true;
                foreach (var usedIndex in samplerIndexes)
                {
                    if (usedIndex != samplerIndex) continue;

                    isFreeSamplerUnit = false;
                    break;
                }

                if (!isFreeSamplerUnit) continue;

                BindTexture(textures[i], samplerIndex, TextureTarget.Texture2D, activate);
                samplerIndexes[i] = samplerIndex;
                lastRecycledTextureUnit = samplerIndex;
                break;
            }
        }

        return samplerIndexes[0];
    }

    public static void UnbindTexture(int textureId)
    {
        var i = Array.IndexOf(samplerTextureIds, textureId, 0, samplerTextureIds.Length);
        if (i == -1) return;

        samplerTextureIds[i] = 0;

        ActiveTextureUnit = i;
        GL.BindTexture(samplerTexturingModes[i], 0);
    }

    #endregion

    #region Other states

    static Rectangle viewport;

    public static Rectangle Viewport
    {
        get => viewport;
        set
        {
            if (viewport == value) return;

            viewport = value;

            GL.Viewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);
            ViewportChanged?.Invoke();
        }
    }

    public static event Action ViewportChanged;

    static Rectangle? clipRegion;

    public static Rectangle? ClipRegion
    {
        get => clipRegion;
        set
        {
            if (clipRegion == value) return;

            FlushRenderer();
            clipRegion = value;
        }
    }

    static Rectangle? Clip(Rectangle? newRegion)
    {
        var previousClipRegion = clipRegion;
        ClipRegion = clipRegion.HasValue && newRegion.HasValue ?
            Rectangle.Intersect(Nullable.GetValueRefOrDefaultRef(ref clipRegion),
                Nullable.GetValueRefOrDefaultRef(ref newRegion)) :
            newRegion;

        return previousClipRegion;
    }

    public static Rectangle? Clip(RectangleF bounds, ICamera camera)
    {
        var screenBounds = camera.ToScreen(bounds);
        return Clip(new((int)float.Round(screenBounds.X),
            viewport.Height - (int)float.Round(screenBounds.Y + screenBounds.Height),
            (int)float.Round(screenBounds.Width),
            (int)float.Round(screenBounds.Height)));
    }

    public static RectangleF? GetClipRegion(ICamera camera)
    {
        if (!clipRegion.HasValue) return null;

        var bounds = camera.FromScreen(Nullable.GetValueRefOrDefaultRef(ref clipRegion));
        return RectangleF.FromLTRB(bounds.X,
            camera.ExtendedViewport.Height - bounds.Bottom,
            bounds.Right,
            camera.ExtendedViewport.Height - bounds.Y);
    }

    static int programId;

    public static int ProgramId
    {
        get => programId;
        set
        {
            if (programId == value) return;

            programId = value;
            GL.UseProgram(programId);
        }
    }

    static readonly PooledDictionary<EnableCap, bool> capabilityCache = new();

    internal static void SetCapability(EnableCap capability, bool enable)
    {
        if (capabilityCache.TryGetValue(capability, out var isEnabled) && isEnabled == enable) return;

        if (enable) GL.Enable(capability);
        else GL.Disable(capability);

        capabilityCache[capability] = enable;
    }

    #endregion

    #region Utilities

    public static TextGenerator TextGenerator { get; private set; }
    public static TextFontManager TextFontManager { get; private set; }

    static Version glVer;

    static void retrieveRendererInfo()
    {
        var glVerStr = GL.GetString(StringName.Version);
        glVer = new(glVerStr.Split(' ')[0]);
        SDL.LogInfo(SDL.LogCategory.Render, $"OpenGL v{glVerStr}");

        var rendererName = GL.GetString(StringName.Renderer);
        var rendererVendor = GL.GetString(StringName.Vendor);
        SDL.LogInfo(SDL.LogCategory.Render, $"Renderer: {rendererName} | Vendor: {rendererVendor}");

        if (glVer < new Version(3, 3))
            throw new NotSupportedException(
                $"This application requires at least OpenGL 3.3 (version {glVer} found)\n{rendererName} ({rendererVendor})");

        SDL.LogInfo(SDL.LogCategory.Render, $"GLSL v{GL.GetString(StringName.ShadingLanguageVersion)}");
    }

    #endregion
}

public enum BlendingMode
{
    Off, AlphaBlend, Color, Additive, BlendAdd, Premultiply, Premultiplied
}