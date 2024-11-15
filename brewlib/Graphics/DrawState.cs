namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Cameras;
using Data;
using OpenTK.Graphics.OpenGL;
using Renderers;
using Text;
using Textures;
using Util;

public static class DrawState
{
    public static readonly bool UseSrgb;

    static Renderer renderer;

    static bool flushingRenderer;

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
        retrieveRendererInfo();
        SetCapability(EnableCap.Lighting, false);

        if (UseSrgb && HasCapabilities(3, 0, "GL_ARB_framebuffer_object"))
        {
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer, FramebufferAttachment.BackLeft,
                FramebufferParameterName.FramebufferAttachmentColorEncoding, out var defaultFramebufferColorEncoding);

            if (defaultFramebufferColorEncoding == 0x8C40)
            {
                SetCapability(EnableCap.FramebufferSrgb, true);
                ColorCorrected = true;
            }
            else Trace.TraceWarning("The default framebuffer isn't sRgb");
        }

        // glActiveTexture requires opengl 1.3
        maxFpTextureUnits = HasCapabilities(1, 3) ? GL.GetInteger(GetPName.MaxTextureUnits) : 1;
        maxTextureImageUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);
        maxVertexTextureImageUnits = GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
        maxGeometryTextureImageUnits = HasCapabilities(3, 2, "GL_ARB_geometry_shader4") ?
            GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) :
            0;

        maxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        maxTextureCoords = GL.GetInteger(GetPName.MaxTextureCoords);
        MaxTextureSize = GL.GetInteger(GetPName.MaxTextureSize);

        Trace.WriteLine(
            $"texture units available: fp:{maxFpTextureUnits} ps:{maxTextureImageUnits} vs:{maxVertexTextureImageUnits} gs:{maxGeometryTextureImageUnits} combined:{maxCombinedTextureImageUnits} coords:{maxTextureCoords}");

        Trace.WriteLine($"max texture size: {MaxTextureSize}");

        samplerTextureIds = new int[maxTextureImageUnits];
        samplerTexturingModes = new TexturingModes[maxTextureImageUnits];

        CheckError("initializing openGL context");

        WhitePixel = Texture2d.Create(Color.White, "whitepixel");
        NormalPixel = Texture2d.Create(Color.FromArgb(127, 127, 255), "normalpixel");

        TextGenerator = new(resourceContainer);
        TextFontManager = new();

        Viewport = new(0, 0, width, height);
    }

    public static void Cleanup()
    {
        NormalPixel.Dispose();
        WhitePixel.Dispose();
        TextFontManager.Dispose();
        TextGenerator.Dispose();
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
        Renderer = Unsafe.As<T, Renderer>(ref renderer);
        renderer.Camera = camera;
        renderStates?.Apply();
        return renderer;
    }

    #region Texture states

    public static Texture2d WhitePixel { get; private set; }
    public static Texture2d NormalPixel { get; private set; }

    static int[] samplerTextureIds;
    static TexturingModes[] samplerTexturingModes;

    static int activeTextureUnit, lastRecycledTextureUnit = -1, maxFpTextureUnits, maxTextureImageUnits,
        maxVertexTextureImageUnits, maxGeometryTextureImageUnits, maxCombinedTextureImageUnits, maxTextureCoords;

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

    public static void SetTexturingMode(int samplerIndex, TexturingModes mode)
    {
        var previousMode = samplerTexturingModes[samplerIndex];
        if (previousMode == mode) return;

        if (samplerTextureIds[samplerIndex] != 0) UnbindTexture(samplerTextureIds[samplerIndex]);
        if (samplerIndex < maxFpTextureUnits)
        {
            ActiveTextureUnit = samplerIndex;
            if (previousMode is not TexturingModes.None) SetCapability((EnableCap)ToTextureTarget(previousMode), false);
            if (mode is not TexturingModes.None) SetCapability((EnableCap)ToTextureTarget(mode), true);
        }

        samplerTexturingModes[samplerIndex] = mode;
    }

    public static void BindTexture(int textureId, int samplerIndex = 0, TexturingModes mode = TexturingModes.Texturing2d)
    {
        if (textureId == 0) throw new ArgumentException("Use UnbindTexture instead");

        SetTexturingMode(samplerIndex, mode);
        ActiveTextureUnit = samplerIndex;

        if (samplerTextureIds[samplerIndex] == textureId) return;

        GL.BindTexture(ToTextureTarget(mode), textureId);
        samplerTextureIds[samplerIndex] = textureId;
    }
    public static int BindTexture(BindableTexture texture)
    {
        var samplerUnit = -1;
        var samplerCount = samplerTextureIds.Length;

        var textureId = texture.TextureId;

        for (var samplerIndex = 0; samplerIndex < samplerCount; ++samplerIndex)
            if (samplerTextureIds[samplerIndex] == textureId)
            {
                samplerUnit = samplerIndex;
                break;
            }

        if (samplerUnit != -1) return samplerUnit;

        var first = true;
        var samplerStartIndex = (lastRecycledTextureUnit + 1) % samplerCount;

        for (var samplerIndex = samplerStartIndex; first || samplerIndex != samplerStartIndex;
            samplerIndex = (samplerIndex + 1) % samplerCount)
        {
            first = false;
            if (samplerUnit == samplerIndex) continue;

            BindTexture(textureId, samplerIndex);
            samplerUnit = samplerIndex;
            lastRecycledTextureUnit = samplerIndex;
            break;
        }

        return samplerUnit;
    }

    public static void UnbindTexture(int textureId)
    {
        for (var i = 0; i < samplerTextureIds.Length; ++i)
            if (samplerTextureIds[i] == textureId)
            {
                ActiveTextureUnit = i;
                GL.BindTexture(ToTextureTarget(samplerTexturingModes[i]), 0);
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
        private set
        {
            if (clipRegion == value) return;

            FlushRenderer();
            clipRegion = value;

            SetCapability(EnableCap.ScissorTest, clipRegion.HasValue);
            if (clipRegion.HasValue)
            {
                var actualClipRegion = Rectangle.Intersect(clipRegion.Value, viewport);
                GL.Scissor(actualClipRegion.X, actualClipRegion.Y, actualClipRegion.Width, actualClipRegion.Height);
            }
        }
    }

    public static IDisposable Clip(Rectangle? newRegion)
    {
        var previousClipRegion = clipRegion;
        ClipRegion = clipRegion.HasValue && newRegion.HasValue ?
            Rectangle.Intersect(clipRegion.Value, newRegion.Value) :
            newRegion;

        return new ActionDisposable(() => ClipRegion = previousClipRegion);
    }
    public static IDisposable Clip(RectangleF bounds, Camera camera)
    {
        var screenBounds = camera.ToScreen(bounds);
        return Clip(new((int)MathF.Round(screenBounds.Left),
            viewport.Height - (int)MathF.Round(screenBounds.Top + screenBounds.Height), (int)MathF.Round(screenBounds.Width),
            (int)MathF.Round(screenBounds.Height)));
    }
    public static RectangleF? GetClipRegion(Camera camera)
    {
        if (!clipRegion.HasValue) return null;

        var bounds = camera.FromScreen(clipRegion.Value);
        return RectangleF.FromLTRB(bounds.Left, camera.ExtendedViewport.Height - bounds.Bottom, bounds.Right,
            camera.ExtendedViewport.Height - bounds.Top);
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
    static string[] supportedExtensions;
    static string rendererName, rendererVendor;

    static void retrieveRendererInfo()
    {
        CheckError("initializing");

        var glVerStr = GL.GetString(StringName.Version);
        glVer = new(glVerStr.Split(' ')[0]);
        CheckError("retrieving openGL version");
        Trace.WriteLine($"OpenGL v{glVerStr}");

        rendererName = GL.GetString(StringName.Renderer);
        rendererVendor = GL.GetString(StringName.Vendor);
        CheckError("retrieving renderer information");
        Trace.WriteLine($"Renderer: {rendererName} | Vendor: {rendererVendor}");

        if (!HasCapabilities(2, 0))
            throw new NotSupportedException(
                $"This application requires at least OpenGL 2.0 (version {glVer} found)\n{rendererName} ({rendererVendor})");

        CheckError("retrieving GLSL version");
        Trace.WriteLine($"GLSL v{GL.GetString(StringName.ShadingLanguageVersion)}");

        var extensionsString = GL.GetString(StringName.Extensions);
        supportedExtensions = extensionsString.Split(' ');
        CheckError("retrieving extensions");
        // Trace.WriteLine($"extensions: {extensionsString}");
    }

    public static bool HasCapabilities(int major, int minor, params string[] extensions)
        => glVer >= new Version(major, minor) || HasExtensions(extensions);

    public static bool HasExtensions(params string[] extensions)
        => extensions.All(t => Array.BinarySearch(supportedExtensions, t) >= 0);

    public static TextureTarget ToTextureTarget(TexturingModes mode) => mode switch
    {
        TexturingModes.Texturing2d => TextureTarget.Texture2D,
        TexturingModes.Texturing3d => TextureTarget.Texture3D,
        _ => throw new InvalidOperationException("Not texture target matches the texturing mode " + mode)
    };

    public static void CheckError(string context = null, bool alwaysThrow = false)
    {
        var error = GL.GetError();
        if (alwaysThrow || error != ErrorCode.NoError)
            throw new InvalidOperationException((context is not null ? "OpenGL error while " + context : "OpenGL error") +
                (error != ErrorCode.NoError ? ": " + error : ""));
    }

    #endregion
}

public enum BlendingMode
{
    Off, AlphaBlend, Color,
    Additive, BlendAdd, Premultiply,
    Premultiplied
}

public enum TexturingModes
{
    None, Texturing2d, Texturing3d
}