namespace BrewLib.Graphics;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Cameras;
using Collections.Pooled;
using IO;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Renderers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Text;
using Textures;
using Util;

public static class DrawState
{
    public static readonly bool UseSrgb;

    static IRenderer renderer;

    static bool flushingRenderer;
    static int drawCalls;
    public static bool UseTextureCompression { get; set; }
    public static bool BindlessTexturesSupported { get; } = GLFW.ExtensionSupported("GL_ARB_bindless_texture");

    public static bool ColorCorrected { get; private set; }
    public static int MaxTextureSize { get; private set; }

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

    public static void Initialize(ResourceContainer resourceContainer,
        TextureContainer textureContainer,
        int width,
        int height)
    {
        if (GLFW.ExtensionSupported("GL_KHR_debug"))
        {
            GL.Enable(EnableCap.DebugOutputSynchronous);
            GL.Khr.DebugMessageCallback((source, type, _, severity, length, message, _) =>
                {
                    var bytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.NullRef<byte>(), message),
                        length);

                    Span<char> chars = stackalloc char[Encoding.UTF8.GetCharCount(bytes)];
                    Encoding.UTF8.GetChars(bytes, chars);

                    var str = StringHelper.StringBuilderPool.Retrieve();
                    str.Append("[OpenGL] ");
                    str.Append(chars);
                    str.Append(" (");

                    switch (source)
                    {
                        case DebugSource.DebugSourceApi: str.Append("Source: API"); break;
                        case DebugSource.DebugSourceWindowSystem: str.Append("Source: Window System"); break;
                        case DebugSource.DebugSourceShaderCompiler: str.Append("Source: Shader Compiler"); break;
                        case DebugSource.DebugSourceThirdParty: str.Append("Source: Third Party"); break;
                        case DebugSource.DebugSourceApplication: str.Append("Source: Application"); break;
                        case DebugSource.DebugSourceOther: str.Append("Source: Other"); break;
                    }

                    str.Append(", ");
                    switch (type)
                    {
                        case DebugType.DebugTypeError: str.Append("Type: Error"); break;
                        case DebugType.DebugTypeDeprecatedBehavior: str.Append("Type: Deprecated Behaviour"); break;
                        case DebugType.DebugTypeUndefinedBehavior: str.Append("Type: Undefined Behaviour"); break;
                        case DebugType.DebugTypePortability: str.Append("Type: Portability"); break;
                        case DebugType.DebugTypePerformance: str.Append("Type: Performance"); break;
                        case DebugType.DebugTypeMarker: str.Append("Type: Marker"); break;
                        case DebugType.DebugTypePushGroup: str.Append("Type: Push Group"); break;
                        case DebugType.DebugTypePopGroup: str.Append("Type: Pop Group"); break;
                        case DebugType.DebugTypeOther: str.Append("Type: Other"); break;
                    }

                    str.Append(", ");
                    switch (severity)
                    {
                        case DebugSeverity.DebugSeverityHigh: str.Append("Severity: High"); break;
                        case DebugSeverity.DebugSeverityMedium: str.Append("Severity: Medium"); break;
                        case DebugSeverity.DebugSeverityLow: str.Append("Severity: Low"); break;
                        case DebugSeverity.DebugSeverityNotification: str.Append("Severity: Notification"); break;
                    }

                    str.Append(")\n");

                    Trace.Write(str);
                    if (severity is DebugSeverity.DebugSeverityHigh) throw new InvalidDataException("OpenGL error: " + str);
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
            else Trace.TraceWarning("The default framebuffer isn't sRgb");
        }

        UseTextureCompression &= GLFW.ExtensionSupported("GL_EXT_texture_compression_s3tc");

        maxTextureImageUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);
        maxVertexTextureImageUnits = GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
        maxGeometryTextureImageUnits = GLFW.ExtensionSupported("GL_ARB_geometry_shader4") ?
            GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) :
            0;

        maxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        MaxTextureSize = GL.GetInteger(GetPName.MaxTextureSize);

        Trace.WriteLine(
            $"texture units available: ps:{maxTextureImageUnits} vs:{maxVertexTextureImageUnits} gs:{maxGeometryTextureImageUnits} combined:{maxCombinedTextureImageUnits}");

        Trace.WriteLine($"max texture size: {MaxTextureSize}");

        if (!BindlessTexturesSupported)
        {
            samplerTextureIds = new int[maxTextureImageUnits];
            samplerTexturingModes = new TextureTarget[maxTextureImageUnits];
        }

        WhitePixel = Texture2d.Create(Color.White.ToPixel<Rgba32>());
        TransparentPixel = Texture2d.Create(default);

        TextGenerator = new(resourceContainer);
        TextFontManager = new(textureContainer);

        Viewport = new(0, 0, width, height);

        TextureUploadQueue.Initialize();
    }

    public static void Cleanup()
    {
        WhitePixel.Dispose();
        TextFontManager.Dispose();
        TextGenerator.Dispose();
        capabilityCache.Dispose();
        TextureUploadQueue.Cleanup();
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

    static int lastRecycledTextureUnit = -1, maxTextureImageUnits, maxVertexTextureImageUnits, maxGeometryTextureImageUnits,
        maxCombinedTextureImageUnits;

    static void SetTexturingMode(int samplerIndex, TextureTarget mode)
    {
        ref var previousMode = ref samplerTexturingModes[samplerIndex];
        if (previousMode == mode) return;

        if (samplerTextureIds[samplerIndex] != 0) UnbindTexture(samplerTextureIds[samplerIndex]);
        previousMode = mode;
    }

    static void BindTexture(int textureId, int samplerIndex, TextureTarget mode = TextureTarget.Texture2D)
    {
        SetTexturingMode(samplerIndex, mode);

        ref var samplerTextureId = ref samplerTextureIds[samplerIndex];
        if (samplerTextureId == textureId) return;

        GL.BindTextureUnit(samplerIndex, textureId);
        samplerTextureId = textureId;
    }

    public static int BindTexture(int textureId) => BindTextures([textureId]);

    static int BindTextures(ReadOnlySpan<int> textures)
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
            for (var samplerIndex = samplerStartIndex;
                first || samplerIndex != samplerStartIndex;
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

                BindTexture(textures[i], samplerIndex);
                samplerIndexes[i] = samplerIndex;
                lastRecycledTextureUnit = samplerIndex;
                break;
            }
        }

        return samplerIndexes[0];
    }

    static void UnbindTexture(int textureId)
    {
        var i = Array.IndexOf(samplerTextureIds, textureId, 0, samplerTextureIds.Length);
        if (i == -1) return;

        GL.BindTextureUnit(i, 0);
        samplerTextureIds[i] = 0;
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
        Trace.WriteLine($"OpenGL v{glVerStr}");

        var rendererName = GL.GetString(StringName.Renderer);
        var rendererVendor = GL.GetString(StringName.Vendor);
        Trace.WriteLine($"Renderer: {rendererName} | Vendor: {rendererVendor}");

        if (glVer < new Version(3, 3))
            throw new NotSupportedException(
                $"This application requires at least OpenGL 3.3 (version {glVer} found)\n{rendererName} ({rendererVendor})");

        Trace.WriteLine($"GLSL v{GL.GetString(StringName.ShadingLanguageVersion)}");
    }

    #endregion
}

public enum BlendingMode
{
    Off, AlphaBlend, Color, Additive, BlendAdd, Premultiply, Premultiplied
}