using BrewLib.Data;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Text;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace BrewLib.Graphics
{
    public static class DrawState
    {
        public const bool UseSrgb = false;

        public static int MaxDrawBuffers { get; private set; }
        public static bool ColorCorrected { get; private set; }
        public static int TextureBinds { get; private set; }
        public static int MaxTextureSize { get; private set; }

        public static void Initialize(ResourceContainer resourceContainer, int width, int height)
        {
            retrieveRendererInfo();
            setupDebugOutput();

            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);
            SetCapability(EnableCap.Lighting, false);

            if (UseSrgb && HasCapabilities(3, 0, "GL_ARB_framebuffer_object"))
            {
                GL.GetFramebufferAttachmentParameter(FramebufferTarget.Framebuffer, FramebufferAttachment.BackLeft, FramebufferParameterName.FramebufferAttachmentColorEncoding, out int defaultFramebufferColorEncoding);
                if (defaultFramebufferColorEncoding == 0x8C40)
                {
                    SetCapability(EnableCap.FramebufferSrgb, true);
                    ColorCorrected = true;
                }
                else Trace.WriteLine("Warning: The default framebuffer isn't sRgb");
            }

            // glActiveTexture requires opengl 1.3
            maxFpTextureUnits = HasCapabilities(1, 3) ? GL.GetInteger(GetPName.MaxTextureUnits) : 1;
            maxTextureImageUnits = GL.GetInteger(GetPName.MaxTextureImageUnits);
            maxVertexTextureImageUnits = GL.GetInteger(GetPName.MaxVertexTextureImageUnits);
            maxGeometryTextureImageUnits = HasCapabilities(3, 2, "GL_ARB_geometry_shader4") ? GL.GetInteger(GetPName.MaxGeometryTextureImageUnits) : 0;
            maxCombinedTextureImageUnits = GL.GetInteger(GetPName.MaxCombinedTextureImageUnits);
            maxTextureCoords = GL.GetInteger(GetPName.MaxTextureCoords);
            MaxTextureSize = GL.GetInteger(GetPName.MaxTextureSize);

            // glDrawBuffers requires opengl 2.0
            MaxDrawBuffers = HasCapabilities(2, 0) ? GL.GetInteger(GetPName.MaxDrawBuffers) : 1;

            Trace.WriteLine($"texture units available: fp:{maxFpTextureUnits} ps:{maxTextureImageUnits} vs:{maxVertexTextureImageUnits} gs:{maxGeometryTextureImageUnits} combined:{maxCombinedTextureImageUnits} coords:{maxTextureCoords}");
            Trace.WriteLine($"max texture size: {MaxTextureSize}");

            samplerTextureIds = new int[maxTextureImageUnits];
            samplerTexturingModes = new TexturingModes[maxTextureImageUnits];

            CheckError("initializing openGL context");

            whitePixel = Texture2d.Create(Color4.White, "whitepixel");
            normalPixel = Texture2d.Create(new Color4(.5f, .5f, 1, 1), "normalpixel", 1, 1, new TextureOptions { Srgb = false });
            textGenerator = new TextGenerator(resourceContainer);
            textFontManager = new TextFontManager();

            Viewport = new Rectangle(0, 0, width, height);
        }
        public static void Cleanup()
        {
            normalPixel.Dispose();
            normalPixel = null;

            whitePixel.Dispose();
            whitePixel = null;

            textFontManager.Dispose();
            textFontManager = null;

            textGenerator.Dispose();
            textGenerator = null;
        }
        public static void CompleteFrame()
        {
            Renderer = null;

            capabilityCache.Clear();
            RenderStates.ClearStateCache();
        }

        static Renderer renderer;
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

        static bool flushingRenderer;
        public static void FlushRenderer(bool canBuffer = false)
        {
            if (renderer == null || flushingRenderer) return;

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

        static Texture2d whitePixel;
        public static Texture2d WhitePixel => whitePixel;

        static Texture2d normalPixel;
        public static Texture2d NormalPixel => normalPixel;

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

        static int maxFpTextureUnits;
        static int maxTextureImageUnits;
        static int maxVertexTextureImageUnits;
        static int maxGeometryTextureImageUnits;
        static int maxCombinedTextureImageUnits;
        static int maxTextureCoords;

        public static void SetTexturingMode(int samplerIndex, TexturingModes mode)
        {
            var previousMode = samplerTexturingModes[samplerIndex];
            if (previousMode == mode) return;

            if (samplerTextureIds[samplerIndex] != 0) UnbindTexture(samplerTextureIds[samplerIndex]);
            if (samplerIndex < maxFpTextureUnits)
            {
                ActiveTextureUnit = samplerIndex;
                if (previousMode != TexturingModes.None) SetCapability((EnableCap)ToTextureTarget(previousMode), false);
                if (mode != TexturingModes.None) SetCapability((EnableCap)ToTextureTarget(mode), true);
            }

            samplerTexturingModes[samplerIndex] = mode;
        }

        public static void BindPrimaryTexture(int textureId, TexturingModes mode = TexturingModes.Texturing2d) => BindTexture(textureId, 0, mode);
        public static void BindTexture(int textureId, int samplerIndex = 0, TexturingModes mode = TexturingModes.Texturing2d)
        {
            if (textureId == 0) throw new ArgumentException("Use UnbindTexture instead");

            SetTexturingMode(samplerIndex, mode);
            ActiveTextureUnit = samplerIndex;

            if (samplerTextureIds[samplerIndex] != textureId)
            {
                GL.BindTexture(ToTextureTarget(mode), textureId);
                samplerTextureIds[samplerIndex] = textureId;

                ++TextureBinds;
            }
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
            for (int samplerIndex = 0, samplerCount = samplerTextureIds.Length; samplerIndex < samplerCount; ++samplerIndex) if (samplerTextureIds[samplerIndex] == textureId)
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

            for (int textureIndex = 0, textureCount = textures.Length; textureIndex < textureCount; ++textureIndex)
            {
                var textureId = textures[textureIndex].TextureId;

                samplerIndexes[textureIndex] = -1;
                for (var samplerIndex = 0; samplerIndex < samplerCount; ++samplerIndex) if (samplerTextureIds[samplerIndex] == textureId)
                {
                    samplerIndexes[textureIndex] = samplerIndex;
                    break;
                }
            }
            for (int textureIndex = 0, textureCount = textures.Length; textureIndex < textureCount; ++textureIndex)
            {
                if (samplerIndexes[textureIndex] != -1) continue;

                var texture = textures[textureIndex];
                int textureId = texture.TextureId;

                var first = true;
                var samplerStartIndex = (lastRecycledTextureUnit + 1) % samplerCount;
                for (var samplerIndex = samplerStartIndex; first || samplerIndex != samplerStartIndex; samplerIndex = (samplerIndex + 1) % samplerCount)
                {
                    first = false;

                    bool isFreeSamplerUnit = true;
                    for (var i = 0; i < samplerIndexes.Length; ++i) if (samplerIndexes[i] == samplerIndex)
                    {
                        isFreeSamplerUnit = false;
                        break;
                    }

                    if (isFreeSamplerUnit)
                    {
                        BindTexture(textureId, samplerIndex, texture.TexturingMode);
                        samplerIndexes[textureIndex] = samplerIndex;
                        lastRecycledTextureUnit = samplerIndex;
                        break;
                    }
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

                GL.Viewport(viewport);
                ViewportChanged?.Invoke();
            }
        }
        public delegate void ViewportChangedEvent();
        public static event ViewportChangedEvent ViewportChanged;

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
            ClipRegion = clipRegion.HasValue && newRegion.HasValue ? Rectangle.Intersect(clipRegion.Value, newRegion.Value) : newRegion;
            return new ActionDisposable(() => ClipRegion = previousClipRegion);
        }
        public static IDisposable Clip(Box2 bounds, Camera camera)
        {
            var screenBounds = camera.ToScreen(RectangleF.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
            var clipRectangle = new Rectangle(
                (int)Math.Round(screenBounds.Left),
                viewport.Height - (int)Math.Round(screenBounds.Top + screenBounds.Height),
                (int)Math.Round(screenBounds.Width),
                (int)Math.Round(screenBounds.Height));

            return Clip(clipRectangle);
        }
        public static Box2? GetClipRegion(Camera camera)
        {
            if (!clipRegion.HasValue) return null;

            var bounds = camera.FromScreen(RectangleF.FromLTRB(clipRegion.Value.Left, clipRegion.Value.Top, clipRegion.Value.Right, clipRegion.Value.Bottom));
            var clipRectangle = new Box2(
                bounds.Left, camera.ExtendedViewport.Height - bounds.Bottom,
                bounds.Right, camera.ExtendedViewport.Height - bounds.Top);

            return clipRectangle;
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
            if (capabilityCache.TryGetValue(capability, out bool isEnabled) && isEnabled == enable) return;

            if (enable) GL.Enable(capability);
            else GL.Disable(capability);

            capabilityCache[capability] = enable;
        }

        #endregion

        #region Utilities

        static TextGenerator textGenerator;
        public static TextGenerator TextGenerator => textGenerator;

        static TextFontManager textFontManager;
        public static TextFontManager TextFontManager => textFontManager;

        static Version openGlVersion, glslVersion;
        static string[] supportedExtensions;
        static string rendererName, rendererVendor;

        static void retrieveRendererInfo()
        {
            CheckError("initializing");

            var openGlVersionString = GL.GetString(StringName.Version);
            openGlVersion = new Version(openGlVersionString.Split(' ')[0]);
            CheckError("retrieving openGL version");
            Trace.WriteLine($"gl version: {openGlVersionString}");

            rendererName = GL.GetString(StringName.Renderer);
            rendererVendor = GL.GetString(StringName.Vendor);
            CheckError("retrieving renderer information");
            Trace.WriteLine($"renderer: {rendererName}, vendor: {rendererVendor}");

            if (!HasCapabilities(2, 0)) throw new NotSupportedException($"This application requires at least OpenGL 2.0 (version {openGlVersion} found)\n{rendererName} ({rendererVendor})");

            var glslVersionString = GL.GetString(StringName.ShadingLanguageVersion);
            glslVersion = string.IsNullOrEmpty(glslVersionString) ? new Version() : new Version(glslVersionString.Split(' ')[0]);
            CheckError("retrieving glsl version");
            Trace.WriteLine($"glsl version: {glslVersionString}");

            var extensionsString = GL.GetString(StringName.Extensions);
            supportedExtensions = extensionsString.Split(' ');
            CheckError("retrieving extensions");
            // Trace.WriteLine($"extensions: {extensionsString}");
        }
        static void setupDebugOutput()
        {
#if !true // because vscode is stupid
            if (!HasCapabilities(4, 3, "GL_KHR_debug"))
            {
                Trace.WriteLine("openGL debug output is unavailable");
                return;
            }

            Trace.WriteLine("\nenabling openGL debug output");

            SetCapability(EnableCap.DebugOutput, true);
            SetCapability(EnableCap.DebugOutputSynchronous, true);
            CheckError("enabling debug output");

            openGLDebugDelegate = new DebugProc(openGLDebugCallback);

            GL.DebugMessageCallback(openGLDebugDelegate, nint.Zero);
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0, new int[0], true);
            CheckError("setting up debug output");

            GL.DebugMessageInsert(DebugSourceExternal.DebugSourceApplication, DebugType.DebugTypeMarker, 0, DebugSeverity.DebugSeverityNotification, -1, "Debug output enabled");
            CheckError("testing debug output");
#endif
        }

        public static bool HasCapabilities(int major, int minor, params string[] extensions) => openGlVersion >= new Version(major, minor) || HasExtensions(extensions);
        public static bool HasExtensions(params string[] extensions)
        {
            for (var i = 0; i < extensions.Length; ++i) if (!supportedExtensions.Contains(extensions[i])) return false;
            return true;
        }
        public static bool HasShaderCapabilities(int major, int minor) => glslVersion >= new Version(major, minor);

        public static TextureTarget ToTextureTarget(TexturingModes mode) => mode switch
        {
            TexturingModes.Texturing2d => TextureTarget.Texture2D,
            TexturingModes.Texturing3d => TextureTarget.Texture3D,
            _ => throw new InvalidOperationException("Not texture target matches the texturing mode " + mode),
        };
        public static void CheckError(string context = null, bool alwaysThrow = false)
        {
            var error = GL.GetError();
            if (alwaysThrow || error != ErrorCode.NoError) throw new GraphicsErrorException(
                (context != null ? "openGL error while " + context : "openGL error") +
                (error != ErrorCode.NoError ? ": " + error.ToString() : string.Empty));
        }

        #endregion
    }
    public enum BlendingMode
    {
        Off,
        Alphablend, Color, Additive, BlendAdd,
        Premultiply, Premultiplied
    }
    public enum TexturingModes
    {
        None, Texturing2d, Texturing3d
    }
}