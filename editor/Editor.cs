namespace StorybrewEditor;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using BrewLib.Data;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Drawables;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Input;
using BrewLib.ScreenLayers;
using BrewLib.Time;
using BrewLib.UserInterface;
using BrewLib.UserInterface.Skinning;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using ScreenLayers;
using Label = BrewLib.UserInterface.Label;
using NativeWindow = OpenTK.Windowing.Desktop.NativeWindow;

public sealed class Editor(NativeWindow window) : IDisposable
{
    readonly FrameClock clock = new();
    public readonly IWin32Window FormsWindow = new DialogParent();

    DrawContext drawContext;
    public InputManager InputManager;

    public bool IsFixedRateUpdate;
    public ResourceContainer ResourceContainer;
    ScreenLayerManager screenLayerManager;
    public Skin Skin;
    public FrameTimeSource TimeSource => clock;

    public void Dispose()
    {
        window.Resize -= resizeToWindow;
        window.Closing -= window_Closing;

        screenLayerManager.Dispose();
        overlay.Dispose();
        overlayCamera.Dispose();
        InputManager.Dispose();
        drawContext.Dispose();
        Skin.Dispose();
        DrawState.Cleanup();
    }

    public void Initialize(ScreenLayer initialLayer = null)
    {
        ResourceContainer =
            new AssemblyResourceContainer(typeof(Editor).Assembly, $"{nameof(StorybrewEditor)}.Resources", "resources");

        var size = window.ClientSize;
        DrawState.UseTextureCompression = Program.Settings.TextureCompression;
        DrawState.Initialize(ResourceContainer, size.X, size.Y);

        drawContext = new();
        drawContext.Register(this);
        drawContext.Register<TextureContainer>(new TextureContainerAtlas(ResourceContainer, null, 1024, 1024), true);
        drawContext.Register<QuadRenderer>(new QuadRendererBuffered(), true);
        drawContext.Register<LineRenderer>(new LineRendererBuffered(), true);

        try
        {
            var brewLibAssembly = typeof(Drawable).Assembly;
            Skin = new(drawContext.Get<TextureContainer>())
            {
                ResolveDrawableType =
                    drawableTypeName
                        => brewLibAssembly.GetType(
                            $"{nameof(BrewLib)}.{nameof(BrewLib.Graphics)}.{nameof(BrewLib.Graphics.Drawables)}.{drawableTypeName}",
                            true, true),
                ResolveWidgetType =
                    widgetTypeName
                        => Type.GetType($"{nameof(StorybrewEditor)}.{nameof(UserInterface)}.{widgetTypeName}", false, true) ??
                        brewLibAssembly.GetType($"{nameof(BrewLib)}.{nameof(UserInterface)}.{widgetTypeName}", true, true),
                ResolveStyleType = styleTypeName
                    => Type.GetType(
                        $"{nameof(StorybrewEditor)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}",
                        false, true) ??
                    brewLibAssembly.GetType(
                        $"{nameof(BrewLib)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}",
                        true, true)
            };

            Skin.Load("skin.json", ResourceContainer);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Loading skin: {e}");
            Skin = new(drawContext.Get<TextureContainer>());
        }

        InputDispatcher inputDispatcher = new();
        InputManager = new(window, inputDispatcher);

        screenLayerManager = new(window, clock, this);
        inputDispatcher.Add(createOverlay(screenLayerManager));
        inputDispatcher.Add(screenLayerManager.InputHandler);

        Restart(initialLayer);

        window.Resize += resizeToWindow;
        window.Closing += window_Closing;

        resizeToWindow(new(size));
    }

    public void Restart(ScreenLayer initialLayer = null, string message = null)
    {
        initializeOverlay();
        screenLayerManager.Set(initialLayer ?? new StartMenu());
        if (message is not null) screenLayerManager.ShowMessage(message);
    }

    public void Update(float time, bool isFixedRateUpdate = true)
    {
        IsFixedRateUpdate = isFixedRateUpdate;
        clock.AdvanceFrameTo(time);

        updateOverlay();
        screenLayerManager.Update(IsFixedRateUpdate);
    }

    public void Draw(float tween)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);

        screenLayerManager.Draw(drawContext, tween);
        overlay.Draw(drawContext);
        DrawState.CompleteFrame();
    }

    void window_Closing(CancelEventArgs e) => e.Cancel = screenLayerManager.Close();

    void resizeToWindow(ResizeEventArgs e)
    {
        var width = e.Width;
        var height = e.Height;

        DrawState.Viewport = new(0, 0, width, height);

        var virtualHeight = height * Math.Max(1024f / width, 768f / height);
        overlayCamera.VirtualHeight = (int)virtualHeight;

        var virtualWidth = width * virtualHeight / height;
        overlayCamera.VirtualWidth = (int)virtualWidth;
        overlay.Size = new(virtualWidth, virtualHeight);
    }

    sealed class DialogParent : IWin32Window
    {
        public nint Handle => Native.MainWindowHandle;
    }

    #region Overlay

    WidgetManager overlay;
    CameraOrtho overlayCamera;
    LinearLayout overlayTop, altOverlayTop;
    Slider volumeSlider;
    Label statsLabel;

    WidgetManager createOverlay(ScreenLayerManager manager)
        => overlay = new(manager, InputManager, Skin) { Camera = overlayCamera = new() };

    void initializeOverlay()
    {
        overlay.Root.ClearWidgets();
        overlay.Root.Add(overlayTop = new(overlay)
        {
            AnchorTarget = overlay.Root,
            AnchorFrom = BoxAlignment.Top,
            AnchorTo = BoxAlignment.Top,
            Horizontal = true,
            Opacity = 0,
            Displayed = false,
            Children =
            [
                statsLabel = new(overlay)
                {
                    StyleName = "small",
                    AnchorTarget = overlay.Root,
                    AnchorTo = BoxAlignment.TopLeft,
                    Displayed = Program.Settings.ShowStats
                }
            ]
        });

        overlayTop.Pack(1024, 16);

        overlay.Root.Add(altOverlayTop = new(overlay)
        {
            AnchorTarget = overlay.Root,
            AnchorFrom = BoxAlignment.Top,
            AnchorTo = BoxAlignment.Top,
            Horizontal = true,
            Opacity = 0,
            Displayed = false,
            Children =
            [
                new Label(overlay) { StyleName = "icon", Icon = IconFont.VolumeUp },
                volumeSlider = new(overlay) { Step = .01f }
            ]
        });

        altOverlayTop.Pack(0, 0, 1024);

        Program.Settings.Volume.Bind(volumeSlider, () => volumeSlider.Tooltip = $"Volume: {volumeSlider.Value:P0}");
        overlay.Root.OnMouseWheel += (_, e) =>
        {
            if (!InputManager.AltOnly) return false;

            volumeSlider.Value += e.OffsetY * .05f;
            return true;
        };
    }

    void updateOverlay()
    {
        if (!IsFixedRateUpdate) return;

        var mousePosition = overlay.MousePosition;
        var bounds = altOverlayTop.Bounds;

        var showAltOverlayTop = InputManager.AltOnly ||
            altOverlayTop.Displayed && bounds.Top < mousePosition.Y && mousePosition.Y < bounds.Bottom;

        var altOpacity = altOverlayTop.Opacity;
        var targetOpacity = showAltOverlayTop ? 1f : 0;
        altOpacity = Math.Abs(altOpacity - targetOpacity) <= .07f ?
            targetOpacity :
            Math.Clamp(altOpacity + (altOpacity < targetOpacity ? .07f : -.07f), 0, 1);

        overlayTop.Opacity = 1 - altOpacity;
        overlayTop.Displayed = altOpacity < 1;

        altOverlayTop.Opacity = altOpacity;
        altOverlayTop.Displayed = altOpacity > 0;

        if (statsLabel.Visible) statsLabel.Text = Program.Stats;
    }

    #endregion
}