﻿namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Cameras;
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
    public static bool UseTextureCompression { get; set; }

    public static bool ColorCorrected { get; private set; }
    public static int MaxTextureSize { get; private set; }

    public static Renderer Renderer
    {
        get => renderer;
        set
        {
            if (renderer == value) return;

            FlushRenderer();

            flushingRenderer = true;
            renderer?.EndRendering();

            renderer = value;

            renderer?.BeginRendering();
            flushingRenderer = false;
        }
    }

    public static void Initialize(ResourceContainer resourceContainer, int width, int height)
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

        samplerTextureIds = new int[maxTextureImageUnits];
        samplerTexturingModes = new TextureTarget[maxTextureImageUnits];

        WhitePixel = Texture2d.Create(Color.White.ToPixel<Rgba32>(), "whitepixel");
        NormalPixel = Texture2d.Create(new(127, 127, 255), "normalpixel");

        TextGenerator = new(resourceContainer);
        TextFontManager = new();

        Viewport = new(0, 0, width, height);
    }

    public static void Cleanup()
    {
        NormalPixel.Dispose();
        WhitePixel.Dispose();
        TextFontManager.Dispose();
    }

    public static void CompleteFrame()
    {
        Renderer = null;

        capabilityCache.Clear();
        RenderStates.ClearStateCache();
    }

    public static void FlushRenderer(bool canBuffer = false)
    {
        if (renderer is null || flushingRenderer) return;

        flushingRenderer = true;
        renderer.Flush(canBuffer);
        flushingRenderer = false;
    }

    public static T Prepare<T>(T renderer, Camera camera, RenderStates renderStates) where T : Renderer
    {
        Renderer = renderer;
        renderer.Camera = camera;
        renderStates?.Apply();
        return renderer;
    }

    #region Texture states

    public static Texture2d WhitePixel { get; private set; }
    public static Texture2d NormalPixel { get; private set; }

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
        var samplerCount = samplerTextureIds.Length;

        for (var i = 0; i < textures.Length; ++i)
        {
            var textureId = textures[i];

            samplerIndexes[i] = -1;
            for (var j = 0; j < samplerCount; ++j)
                if (samplerTextureIds[j] == textureId)
                {
                    samplerIndexes[i] = j;
                    break;
                }
        }

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

    public static void UnbindTexture(int textureId)
    {
        for (var i = 0; i < samplerTextureIds.Length; ++i)
            if (samplerTextureIds[i] == textureId)
            {
                GL.BindTextureUnit(i, 0);
                samplerTextureIds[i] = 0;
            }
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

            SetCapability(EnableCap.ScissorTest, clipRegion.HasValue);
            if (!clipRegion.HasValue) return;

            var actualClipRegion = Rectangle.Intersect(Nullable.GetValueRefOrDefaultRef(ref clipRegion), viewport);
            GL.Scissor(actualClipRegion.X, actualClipRegion.Y, actualClipRegion.Width, actualClipRegion.Height);
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

    public static Rectangle? Clip(RectangleF bounds, Camera camera)
    {
        var screenBounds = camera.ToScreen(bounds);
        return Clip(new((int)float.Round(screenBounds.X),
            viewport.Height - (int)float.Round(screenBounds.Y + screenBounds.Height),
            (int)float.Round(screenBounds.Width),
            (int)float.Round(screenBounds.Height)));
    }

    public static RectangleF? GetClipRegion(Camera camera)
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

    static readonly Dictionary<EnableCap, bool> capabilityCache = [];

    internal static void SetCapability(EnableCap capability, bool enable)
    {
        ref var enableRef = ref CollectionsMarshal.GetValueRefOrAddDefault(capabilityCache, capability, out var exists);
        if (!exists && enableRef == enable) return;

        if (enable) GL.Enable(capability);
        else GL.Disable(capability);

        enableRef = enable;
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