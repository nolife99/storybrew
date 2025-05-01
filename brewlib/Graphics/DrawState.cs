namespace BrewLib.Graphics;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

public static class DrawState
{
    public static readonly bool UseSrgb;

    static Renderer renderer;

    static bool flushingRenderer;
    static int drawCalls;
    public static bool UseTextureCompression { get; set; }

    public static bool ColorCorrected { get; private set; }
    public static int MaxTextureSize { get; private set; }

    public static Renderer Renderer
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

    public static void Initialize(TextureContainer textureContainer,
        ResourceContainer resourceContainer,
        int width,
        int height)
    {
        if (GLFW.ExtensionSupported("GL_ARB_debug_output"))
            GL.Arb.DebugMessageCallback((source, type, _, severity, _, message, _) =>
                {
                    var str = Marshal.PtrToStringAnsi(message);
                    Trace.WriteLine("Debug message: " + str);

                    switch (source)
                    {
                        case DebugSource.DebugSourceApi: Trace.WriteLine("Source: API"); break;
                        case DebugSource.DebugSourceWindowSystem: Trace.WriteLine("Source: Window System"); break;
                        case DebugSource.DebugSourceShaderCompiler: Trace.WriteLine("Source: Shader Compiler"); break;
                        case DebugSource.DebugSourceThirdParty: Trace.WriteLine("Source: Third Party"); break;
                        case DebugSource.DebugSourceApplication: Trace.WriteLine("Source: Application"); break;
                        case DebugSource.DebugSourceOther: Trace.WriteLine("Source: Other"); break;
                    }

                    switch (type)
                    {
                        case DebugType.DebugTypeError: Trace.WriteLine("Type: Error"); break;
                        case DebugType.DebugTypeDeprecatedBehavior: Trace.WriteLine("Type: Deprecated Behaviour"); break;
                        case DebugType.DebugTypeUndefinedBehavior: Trace.WriteLine("Type: Undefined Behaviour"); break;
                        case DebugType.DebugTypePortability: Trace.WriteLine("Type: Portability"); break;
                        case DebugType.DebugTypePerformance: Trace.WriteLine("Type: Performance"); break;
                        case DebugType.DebugTypeMarker: Trace.WriteLine("Type: Marker"); break;
                        case DebugType.DebugTypePushGroup: Trace.WriteLine("Type: Push Group"); break;
                        case DebugType.DebugTypePopGroup: Trace.WriteLine("Type: Pop Group"); break;
                        case DebugType.DebugTypeOther: Trace.WriteLine("Type: Other"); break;
                    }

                    switch (severity)
                    {
                        case DebugSeverity.DebugSeverityHigh: Trace.WriteLine("Severity: high"); break;
                        case DebugSeverity.DebugSeverityMedium: Trace.WriteLine("Severity: medium"); break;
                        case DebugSeverity.DebugSeverityLow: Trace.WriteLine("Severity: low"); break;
                        case DebugSeverity.DebugSeverityNotification: Trace.WriteLine("Severity: notification"); break;
                    }

                    if (severity is DebugSeverity.DebugSeverityHigh) throw new InvalidDataException("OpenGL error: " + str);
                },
                0);

        retrieveRendererInfo();
        if (UseSrgb && GLFW.ExtensionSupported("GL_ARB_framebuffer_object"))
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

        // glActiveTexture requires opengl 1.3
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

        using (Image<Rgba32> whitePixel = new(1, 1, Color.White.ToPixel<Rgba32>()))
            WhitePixel = textureContainer.Add(whitePixel, "whitepixel");

        using (Image<Rgba32> transparentPixel = new(1, 1, default))
            TransparentPixel = textureContainer.Add(transparentPixel, "transparentpixel");

        TextGenerator = new(resourceContainer);
        TextFontManager = new(textureContainer);

        Viewport = new(0, 0, width, height);
    }

    public static void Cleanup()
    {
        WhitePixel.Dispose();
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

    public static T Prepare<T>(T nextRenderer, ICamera camera, RenderStates renderStates) where T : Renderer
    {
        Renderer = nextRenderer;
        renderer.Camera = camera;
        renderStates.Apply();
        return nextRenderer;
    }

    #region Texture states

    public static Texture2dRegion WhitePixel { get; private set; }
    public static Texture2dRegion TransparentPixel { get; private set; }

    static int maxTextureImageUnits, maxVertexTextureImageUnits, maxGeometryTextureImageUnits, maxCombinedTextureImageUnits;

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

        if (!GLFW.ExtensionSupported("GL_ARB_direct_state_access"))
            throw new NotSupportedException("This application requires the OpenGL extension 'ARB_direct_state_access'");

        Trace.WriteLine($"GLSL v{GL.GetString(StringName.ShadingLanguageVersion)}");
    }

    #endregion
}

public enum BlendingMode
{
    Off, AlphaBlend, Color, Additive, BlendAdd, Premultiply, Premultiplied
}