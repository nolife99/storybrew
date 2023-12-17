using System;
using System.ComponentModel;
using System.Diagnostics;
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
using osuTK;
using osuTK.Graphics.OpenGL;
using StorybrewEditor.ScreenLayers;

namespace StorybrewEditor;

public sealed class Editor(GameWindow window) : IDisposable
{
    public GameWindow Window => window;
    public readonly System.Windows.Forms.IWin32Window FormsWindow = new DialogParent();

    readonly FrameClock clock = new();
    public FrameTimeSource TimeSource => clock;

    public bool IsFixedRateUpdate;

    DrawContext drawContext;
    public ResourceContainer ResourceContainer;
    public Skin Skin;
    public ScreenLayerManager ScreenLayerManager;
    public InputManager InputManager;

    public void Initialize(ScreenLayer initialLayer = null)
    {
        ResourceContainer = new AssemblyResourceContainer(GetType().Assembly, $"{nameof(StorybrewEditor)}.Resources", "resources");
        DrawState.Initialize(ResourceContainer, Window.Width, Window.Height);

        drawContext = new();
        drawContext.Register(this);
        drawContext.Register<TextureContainer>(new TextureContainerAtlas(ResourceContainer), true);
        drawContext.Register<QuadRenderer>(new QuadRendererBuffered(), true);
        drawContext.Register<LineRenderer>(new LineRendererBuffered(), true);

        try
        {
            var brewLibAssembly = typeof(Drawable).Assembly;
            Skin = new(drawContext.Get<TextureContainer>())
            {
                ResolveDrawableType = drawableTypeName => brewLibAssembly.GetType(
                    $"{nameof(BrewLib)}.{nameof(BrewLib.Graphics)}.{nameof(BrewLib.Graphics.Drawables)}.{drawableTypeName}", true, true),

                ResolveWidgetType = widgetTypeName => Type.GetType(
                    $"{nameof(StorybrewEditor)}.{nameof(UserInterface)}.{widgetTypeName}", false, true) ??
                    brewLibAssembly.GetType($"{nameof(BrewLib)}.{nameof(UserInterface)}.{widgetTypeName}", true, true),

                ResolveStyleType = styleTypeName => Type.GetType(
                    $"{nameof(StorybrewEditor)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}", false, true) ??
                    brewLibAssembly.GetType($"{nameof(BrewLib)}.{nameof(UserInterface)}.{nameof(UserInterface.Skinning)}.{nameof(UserInterface.Skinning.Styles)}.{styleTypeName}", true, true),
            };
            Skin.Load("skin.json", ResourceContainer);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Failed to load skin: {e}");
            Skin = new(drawContext.Get<TextureContainer>());
        }

        InputDispatcher inputDispatcher = new();
        InputManager = new(Window, inputDispatcher);

        ScreenLayerManager = new(Window, clock, this);
        inputDispatcher.Add(createOverlay(ScreenLayerManager));
        inputDispatcher.Add(ScreenLayerManager.InputHandler);

        Restart(initialLayer);

        Window.Resize += window_Resize;
        Window.Closing += window_Closing;

        resizeToWindow();
    }
    public void Restart(ScreenLayer initialLayer = null, string message = null)
    {
        initializeOverlay();
        ScreenLayerManager.Set(initialLayer ?? new StartMenu());
        if (message is not null) ScreenLayerManager.ShowMessage(message);
    }

    #region Overlay

    WidgetManager overlay;
    CameraOrtho overlayCamera;
    LinearLayout overlayTop, altOverlayTop;
    Slider volumeSlider;
    Label statsLabel;

    WidgetManager createOverlay(ScreenLayerManager screenLayerManager) => overlay = new(screenLayerManager, InputManager, Skin)
    {
        Camera = overlayCamera = new()
    };

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
            Children = new Widget[]
            {
                statsLabel = new(overlay)
                {
                    StyleName = "small",
                    AnchorTarget = overlay.Root,
                    AnchorTo = BoxAlignment.TopLeft,
                    Displayed = Program.Settings.ShowStats
                }
            }
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
            Children = new Widget[]
            {
                new Label(overlay)
                {
                    StyleName = "icon",
                    Icon = IconFont.VolumeUp
                },
                volumeSlider = new(overlay)
                {
                    Step = .01f
                }
            }
        });
        altOverlayTop.Pack(0, 0, 1024);

        Program.Settings.Volume.Bind(volumeSlider, () => volumeSlider.Tooltip = $"Volume: {volumeSlider.Value:P0}");
        overlay.Root.OnMouseWheel += (sender, e) =>
        {
            if (!InputManager.AltOnly) return false;

            volumeSlider.Value += e.DeltaPrecise * .05f;
            return true;
        };
    }
    void updateOverlay()
    {
        if (IsFixedRateUpdate)
        {
            var mousePosition = overlay.MousePosition;
            var bounds = altOverlayTop.Bounds;

            var showAltOverlayTop = InputManager.AltOnly || (altOverlayTop.Displayed && bounds.Top < mousePosition.Y && mousePosition.Y < bounds.Bottom);

            var altOpacity = altOverlayTop.Opacity;
            var targetOpacity = showAltOverlayTop ? 1f : 0;
            if (Math.Abs(altOpacity - targetOpacity) <= .07f) altOpacity = targetOpacity;
            else altOpacity = Math.Clamp(altOpacity + (altOpacity < targetOpacity ? .07f : -.07f), 0, 1);

            overlayTop.Opacity = 1 - altOpacity;
            overlayTop.Displayed = altOpacity < 1;

            altOverlayTop.Opacity = altOpacity;
            altOverlayTop.Displayed = altOpacity > 0;

            if (statsLabel.Visible) statsLabel.Text = Program.Stats;
        }
    }

    #endregion

    public void Dispose()
    {
        Window.Resize -= window_Resize;
        Window.Closing -= window_Closing;

        ScreenLayerManager.Dispose();
        overlay.Dispose();
        overlayCamera.Dispose();
        InputManager.Dispose();
        drawContext.Dispose();
        Skin.Dispose();
        DrawState.Cleanup();
    }
    public void Update(double time, bool isFixedRateUpdate = true)
    {
        IsFixedRateUpdate = isFixedRateUpdate;
        clock.AdvanceFrameTo(time);

        updateOverlay();
        ScreenLayerManager.Update(IsFixedRateUpdate);
    }
    public void Draw(double tween)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);

        ScreenLayerManager.Draw(drawContext, tween);
        overlay.Draw(drawContext);
        DrawState.CompleteFrame();
    }

    void window_Resize(object sender, EventArgs e) => resizeToWindow();
    void window_Closing(object sender, CancelEventArgs e) => e.Cancel = ScreenLayerManager.Close();
    void resizeToWindow()
    {
        var width = Window.Width;
        var height = Window.Height;

        DrawState.Viewport = new(0, 0, width, height);

        var virtualHeight = height * Math.Max(1024f / width, 768f / height);
        overlayCamera.VirtualHeight = (int)virtualHeight;

        var virtualWidth = width * virtualHeight / height;
        overlayCamera.VirtualWidth = (int)virtualWidth;
        overlay.Size = new(virtualWidth, virtualHeight);
    }
    class DialogParent : System.Windows.Forms.IWin32Window
    {
        public nint Handle => Native.MainWindowHandle;
    }
}