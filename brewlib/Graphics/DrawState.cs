﻿namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using Cameras;
using Data;
using osuTK.Graphics;
using osuTK.Graphics.OpenGL;
using Renderers;
using Text;
using Textures;
using Util;

public static class DrawState
{
    public static readonly bool UseSrgb;

    static Renderer renderer;

    static bool flushingRenderer;

    public static int MaxDrawBuffers { get; private set; }
    public static bool ColorCorrected { get; private set; }
    public static int TextureBinds { get; private set; }
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
            else
                Trace.TraceWarning("The default framebuffer isn't sRgb");
        }

        // glActiveTexture requires opengl 1.3
        maxFpTextureUnits = HasCapabilities(1, 3) ? GL.GetInteger(GetPName.MaxTextureUnits) : 1;
        maxTextureImageUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);
        maxVertexTextureImageUnits = GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
        maxGeometryTextureImageUnits = HasCapabilities(3, 2, "GL_ARB_geometry_shader4")
            ? GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) : 0;
        maxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
        maxTextureCoords = GL.GetInteger(GetPName.MaxTextureCoords);
        MaxTextureSize = GL.GetInteger(GetPName.MaxTextureSize);

        // glDrawBuffers requires opengl 2.0
        MaxDrawBuffers = HasCapabilities(2, 0) ? GL.GetInteger(GetPName.MaxDrawBuffers) : 1;

        Trace.WriteLine($"texture units available: fp:{maxFpTextureUnits} ps:{maxTextureImageUnits} vs:{
            maxVertexTextureImageUnits} gs:{maxGeometryTextureImageUnits} combined:{maxCombinedTextureImageUnits
            } coords:{maxTextureCoords}");
        Trace.WriteLine($"max texture size: {MaxTextureSize}");

        samplerTextureIds = new int[maxTextureImageUnits];
        samplerTexturingModes = new TexturingModes[maxTextureImageUnits];

        CheckError("initializing openGL context");

        WhitePixel = Texture2d.Create(Color.White, "whitepixel");
        NormalPixel = Texture2d.Create(Color.FromArgb(127, 127, 255), "normalpixel", 1, 1, new() { Srgb = UseSrgb });
        TextGenerator = new(resourceContainer);
        TextFontManager = new();

        Viewport = new(0, 0, width, height);
    }

    public static void Cleanup()
    {
        NormalPixel.Dispose();
        NormalPixel = null;

        WhitePixel.Dispose();
        WhitePixel = null;

        TextFontManager.Dispose();
        TextFontManager = null;

        TextGenerator.Dispose();
        TextGenerator = null;
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

    static int activeTextureUnit;

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

    static int lastRecycledTextureUnit = -1;
    static int[] samplerTextureIds;
    static TexturingModes[] samplerTexturingModes;

    static int maxFpTextureUnits, maxTextureImageUnits, maxVertexTextureImageUnits, maxGeometryTextureImageUnits,
        maxCombinedTextureImageUnits, maxTextureCoords;

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

    public static void BindPrimaryTexture(int textureId, TexturingModes mode = TexturingModes.Texturing2d)
        => BindTexture(textureId, 0, mode);

    public static void BindTexture(int textureId, int samplerIndex = 0,
        TexturingModes mode = TexturingModes.Texturing2d)
    {
        if (textureId == 0) throw new ArgumentException("Use UnbindTexture instead");

        SetTexturingMode(samplerIndex, mode);
        ActiveTextureUnit = samplerIndex;

        if (samplerTextureIds[samplerIndex] == textureId) return;

        GL.BindTexture(ToTextureTarget(mode), textureId);
        samplerTextureIds[samplerIndex] = textureId;
        ++TextureBinds;
    }

    public static int BindTexture(BindableTexture texture, bool activate = false)
    {
        var samplerUnit = BindTextures(texture)[0];
        if (activate) ActiveTextureUnit = samplerUnit;
        return samplerUnit;
    }

    public static void UnbindTexture(BindableTexture texture) => UnbindTexture(texture.TextureId);

    public static void UnbindTexture(int textureId)
    {
        for (var samplerIndex = 0; samplerIndex < samplerTextureIds.Length; ++samplerIndex)
            if (samplerTextureIds[samplerIndex] == textureId)
            {
                ActiveTextureUnit = samplerIndex;
                GL.BindTexture(ToTextureTarget(samplerTexturingModes[samplerIndex]), 0);
                samplerTextureIds[samplerIndex] = 0;
            }
    }

    public static int[] BindTextures(params BindableTexture[] textures)
    {
        var samplerIndexes = new int[textures.Length];
        var samplerCount = samplerTextureIds.Length;

        for (var textureIndex = 0; textureIndex < textures.Length; ++textureIndex)
        {
            var textureId = textures[textureIndex].TextureId;

            samplerIndexes[textureIndex] = -1;
            for (var samplerIndex = 0; samplerIndex < samplerCount; ++samplerIndex)
                if (samplerTextureIds[samplerIndex] == textureId)
                {
                    samplerIndexes[textureIndex] = samplerIndex;
                    break;
                }
        }

        for (var textureIndex = 0; textureIndex < textures.Length; ++textureIndex)
        {
            if (samplerIndexes[textureIndex] != -1) continue;

            var texture = textures[textureIndex];
            var textureId = texture.TextureId;

            var first = true;
            var samplerStartIndex = (lastRecycledTextureUnit + 1) % samplerCount;

            for (var samplerIndex = samplerStartIndex; first || samplerIndex != samplerStartIndex;
                samplerIndex = (samplerIndex + 1) % samplerCount)
            {
                first = false;
                if (samplerIndexes.Any(t => t == samplerIndex)) continue;

                BindTexture(textureId, samplerIndex);
                samplerIndexes[textureIndex] = samplerIndex;
                lastRecycledTextureUnit = samplerIndex;
                break;
            }
        }

        return samplerIndexes;
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
        ClipRegion = clipRegion.HasValue && newRegion.HasValue ? Rectangle.Intersect(clipRegion.Value, newRegion.Value)
            : newRegion;
        return new ActionDisposable(() => ClipRegion = previousClipRegion);
    }

    public static IDisposable Clip(RectangleF bounds, Camera camera)
    {
        var screenBounds = camera.ToScreen(bounds);
        return Clip(new((int)MathF.Round(screenBounds.Left),
            viewport.Height - (int)MathF.Round(screenBounds.Top + screenBounds.Height),
            (int)MathF.Round(screenBounds.Width), (int)MathF.Round(screenBounds.Height)));
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

        if (enable)
            GL.Enable(capability);
        else
            GL.Disable(capability);

        capabilityCache[capability] = enable;
    }

#endregion

#region Utilities

    public static TextGenerator TextGenerator { get; private set; }
    public static TextFontManager TextFontManager { get; private set; }

    static Version openGlVersion;
    static string[] supportedExtensions;
    static string rendererName, rendererVendor;

    static void retrieveRendererInfo()
    {
        CheckError("initializing");

        var openGlVersionString = GL.GetString(StringName.Version);
        openGlVersion = new(openGlVersionString.Split(' ')[0]);
        CheckError("retrieving openGL version");
        Trace.WriteLine($"gl version: {openGlVersionString}");

        rendererName = GL.GetString(StringName.Renderer);
        rendererVendor = GL.GetString(StringName.Vendor);
        CheckError("retrieving renderer information");
        Trace.WriteLine($"renderer: {rendererName}, vendor: {rendererVendor}");

        if (!HasCapabilities(2, 0))
            throw new NotSupportedException($"This application requires at least OpenGL 2.0 (version {openGlVersion
            } found)\n{rendererName} ({rendererVendor})");

        CheckError("retrieving glsl version");
        Trace.WriteLine($"glsl version: {GL.GetString(StringName.ShadingLanguageVersion)}");

        var extensionsString = GL.GetString(StringName.Extensions);
        supportedExtensions = extensionsString.Split(' ');
        CheckError("retrieving extensions");
        // Trace.WriteLine($"extensions: {extensionsString}");
    }

    public static bool HasCapabilities(int major, int minor, params string[] extensions)
        => openGlVersion >= new Version(major, minor) || HasExtensions(extensions);

    public static bool HasExtensions(params string[] extensions)
        => extensions.All(t => Array.BinarySearch(supportedExtensions, t) >= 0);

    public static TextureTarget ToTextureTarget(TexturingModes mode)
        => mode switch
        {
            TexturingModes.Texturing2d => TextureTarget.Texture2D,
            TexturingModes.Texturing3d => TextureTarget.Texture3D,
            _ => throw new InvalidOperationException("Not texture target matches the texturing mode " + mode)
        };

    public static void CheckError(string context = null, bool alwaysThrow = false)
    {
        var error = GL.GetError();
        if (alwaysThrow || error != ErrorCode.NoError)
            throw new GraphicsErrorException((context is not null ? "openGL error while " + context : "openGL error") +
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