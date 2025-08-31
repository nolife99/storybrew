namespace StorybrewEditor;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Audio;
using BrewLib.Util;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using SDL3;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew",
        DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";

    public static readonly Version Version = typeof(Editor).Assembly.GetName().Version;

    public static readonly string FullName = $"{Name} {Version} ({Repository})";

    public static AudioManager AudioManager { get; private set; }
    public static Settings Settings { get; private set; }

    static void Main(string[] args)
    {
        if (args.Length != 0 && handleArguments(args)) return;

        setupLogging();
        startEditor();
    }

    static bool handleArguments(string[] args)
    {
        switch (args[0])
        {
            case "update":
                if (args.Length < 3) return false;

                setupLogging(Path.Combine(args[1], DefaultLogPath), "update.log");

                Updater.Update(args[1], new(args[2]));
                return true;

            case "build":
                setupLogging(null, "build.log");
                Builder.Build();
                return true;
        }

        return false;
    }

    #region Editor

    static void startEditor()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
            throw new InvalidOperationException($"Unable to initialize SDL video subsystem: {SDL.GetError()}");

        var audioCreateTask = Task.Run(() =>
        {
            Settings = new();
            Native.MainThreadScheduler = Schedule;
            Updater.NotifyEditorRun();

            return createAudioManager();
        });

        var primaryDisplay = SDL.GetPrimaryDisplay();
        if (primaryDisplay == 0)
            throw new InvalidOperationException($"Unable to get primary display: {SDL.GetError()}");

        var displayDevice = SDL.GetDesktopDisplayMode(primaryDisplay);
        if (!displayDevice.HasValue)
            throw new InvalidOperationException($"Unable to get display device: {SDL.GetError()}");

        var displayDeviceVal = displayDevice.Value;
        var window = createWindow(displayDeviceVal, out var context);

        using Editor editor = new(window);
        using (NetHelper.Client = new())
        {
            NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
            editor.Initialize(displayDeviceVal);

            var iconSetTask = Native.SetWindowIcon(editor.ResourceContainer, "icon.ico");
            using (AudioManager = audioCreateTask.Result)
                runMainLoop(window,
                    editor,
                    (long)(TimeSpan.TicksPerSecond /
                        (Settings.UpdateRate > 0 ? Settings.UpdateRate : displayDeviceVal.RefreshRate)),
                    (long)(TimeSpan.TicksPerSecond / (Settings.FrameRate > 0 ?
                        Settings.FrameRate :
                        displayDeviceVal.RefreshRate)));

            iconSetTask.Wait();
        }

        SDL.GLDestroyContext(context);
        SDL.GLUnloadLibrary();
        SDL.DestroyWindow(window);
        SDL.Quit();

        Settings.Save();
    }

    static nint createWindow(SDL.DisplayMode displayDevice, out nint glContext)
    {
        if (!SDL.GLLoadLibrary(null)) throw new InvalidOperationException($"Unable to load OpenGL: {SDL.GetError()}");

        const SDL.GLContextFlag debugContext =
#if DEBUG
            SDL.GLContextFlag.Debug | SDL.GLContextFlag.ForwardCompatible;
#else
            SDL.GLContextFlag.ForwardCompatible;

        SDL.GLSetAttribute(SDL.GLAttr.ContextNoError, 1);
#endif

        SDL.GLSetAttribute(SDL.GLAttr.ContextProfileMask, (int)SDL.GLProfile.Core);
        SDL.GLSetAttribute(SDL.GLAttr.ContextFlags, (int)debugContext);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMajorVersion, 3);
        SDL.GLSetAttribute(SDL.GLAttr.ContextMinorVersion, 3);

        ref var format = ref SDL.GetPixelFormatDetails(displayDevice.Format).AsRef<SDL.PixelFormatDetails>();
        SDL.GLSetAttribute(SDL.GLAttr.RedSize, format.RBits);
        SDL.GLSetAttribute(SDL.GLAttr.GreenSize, format.GBits);
        SDL.GLSetAttribute(SDL.GLAttr.BlueSize, format.BBits);
        SDL.GLSetAttribute(SDL.GLAttr.AlphaSize, format.ABits);
        SDL.GLSetAttribute(SDL.GLAttr.DepthSize, 0);

        SDL.LogInfo(SDL.LogCategory.System,
            $"Display info: R{format.RBits} G{format.GBits} B{format.BBits} A{format.ABits}");

        var window = SDL.CreateWindow(Name,
            0,
            0,
            SDL.WindowFlags.OpenGL | SDL.WindowFlags.Resizable | SDL.WindowFlags.Hidden);

        if (window == 0) throw new InvalidOperationException($"Unable to create window: {SDL.GetError()}");

        glContext = SDL.GLCreateContext(window);
        if (glContext == 0) throw new InvalidOperationException($"Unable to create OpenGL context: {SDL.GetError()}");

        if (!SDL.GLMakeCurrent(window, glContext))
            throw new InvalidOperationException($"Unable to bind OpenGL context to window: {SDL.GetError()}");

        SDL.GLSetSwapInterval(0);

        GL.LoadBindings(new SDLBindingsContext());
        Native.InitializeHandle(window);

        SDL.GLResetAttributes();

        return window;
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new() { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(nint window, Editor editor, long fixedRateUpdate, long targetFrame)
    {
        long prev = Stopwatch.GetTimestamp(), fixedRate = 0, avActive = 0, longest = 0, lastStat = 0,
            statsUpdate = targetFrame * 5;

        SDL.ShowWindow(window);
        SDL.SetWindowMouseGrab(window, true);
        SDL.SetWindowMouseGrab(window, false);

        SDL.WindowEvent resize = default;

        var exiting = false;
        SDL.EventFilter filter = (nint _, ref SDL.Event @event) =>
        {
            if (@event.Type == (uint)SDL.EventType.Quit) return exiting = true;
            if (@event.Type != (uint)SDL.EventType.WindowExposed) return false;

            if (SDL.GetWindowSize(window, out var w, out var h) && resize.Data1 != w || resize.Data2 != h)
                editor.InputManager.Handler.OnResize(resize = new() { Data1 = w, Data2 = h });

            Redraw(false);
            return true;
        };

        SDL.AddEventWatch(filter, 0);
        while (!exiting) Redraw(true);

        SDL.RemoveEventWatch(filter, 0);
        return;

        void Redraw(bool pumpEvents)
        {
            var cur = Stopwatch.GetTimestamp();
            var fixedUpdates = 0;

            if (pumpEvents) editor.InputManager.PumpEvents();
            AudioManager.Update(targetFrame);

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates++ < 2)
            {
                fixedRate += fixedRateUpdate;
                editor.Update(fixedRate / (float)TimeSpan.TicksPerSecond);
            }

            var windowFocus = (SDL.GetWindowFlags(window) & SDL.WindowFlags.InputFocus) != 0;
            if (windowFocus && fixedUpdates == 0 && fixedRate < cur && cur < fixedRate + fixedRateUpdate)
                editor.Update(cur / (float)TimeSpan.TicksPerSecond, false);

            var draws = editor.Draw();
            if (!SDL.GLSwapWindow(window))
                throw new InvalidOperationException($"Unable to swap framebuffer: {SDL.GetError()}");

            using (var snapshot = TempList.Create<IDisposable>())
            {
                lock (schedulerLock)
                {
                    snapshot.AddRange(scheduledActions);
                    scheduledActions.Clear();
                }

                foreach (var action in snapshot) action.Dispose();
            }

            var active = Stopwatch.GetTimestamp() - cur;
            var sleepTime = (windowFocus ? targetFrame : fixedRateUpdate) - active;

            if (sleepTime > 0) Native.AccurateSleep(sleepTime);
            else Bass.UpdateThreads = 1;

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + statsUpdate > cur) return;

            if (sleepTime > 0) Bass.UpdateThreads = 0;

            avActive = (active + avActive) / 2;
            longest = long.Max(frameTime, longest);

            buildStatsMessage(editor, frameTime, avActive, longest, draws);

            lastStat = cur;
        }
    }

    static void buildStatsMessage(Editor editor, long av, long avActive, long longest, int draws)
    {
        if (!editor.statsLabel.Visible) return;

        const float ticks = TimeSpan.TicksPerSecond;
        const float millis = TimeSpan.TicksPerMillisecond;

        using var result = StringHelper.Interpolate(CultureInfo.InvariantCulture,
            $"{ticks / av:f0}/{ticks / avActive:f0}fps (act:{avActive / millis:f2} avg:{av / millis:f2} hi:{longest / millis:f2})\n{draws} draws");

        editor.statsLabel.Text = result.AsReadOnlySpan();
    }

    #endregion

    #region Scheduling

    static readonly Lock schedulerLock = new();
    static readonly List<IDisposable> scheduledActions = [];

    public static ValueTask Schedule<TState>(Action<TState> action, TState state = default)
    {
        if (SDL.IsMainThread())
        {
            action(state);
            return ValueTask.CompletedTask;
        }

        var tcs = ValueTaskSourceHolder<TState>.Get(action, state);
        lock (schedulerLock) scheduledActions.Add(tcs);

        return tcs.Task;
    }

    #endregion

    #region Error Handling

    const string DefaultLogPath = "logs";

    static readonly Lock errorHandlerLock = new();
    static bool insideErrorHandler;

    static void setupLogging(string logsPath = null, string commonLogFilename = null)
    {
        SDL.SetLogPriorities(SDL.LogPriority.Trace);

        logsPath ??= DefaultLogPath;

        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");
        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");
        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else if (File.Exists(exceptionPath)) File.Delete(exceptionPath);

        SDL.LogInfo(SDL.LogCategory.Application, FullName);

        var domain = AppDomain.CurrentDomain;
        domain.FirstChanceException += (_, e) => logError(e.Exception, exceptionPath, false);
        domain.UnhandledException += (_, e) => logError((Exception)e.ExceptionObject, crashPath, e.IsTerminating);
    }

    static void logError(Exception e, string filename, bool show)
    {
        lock (errorHandlerLock)
        {
            if (insideErrorHandler) return;

            insideErrorHandler = true;

            using StreamWriter w = new(Path.Combine(Environment.CurrentDirectory, filename), true);
            try
            {
                w.Write(DateTimeOffset.Now + " - ");
                w.WriteLine(e);
                w.WriteLine();

                Trace.Flush();

                if (!show) return;

                using MessageBoxData data = new(MessageBoxFlags.Error,
                    0,
                    FullName,
                    $"An error occurred:\n{e.Message} ({e.GetType().Name})\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.",
                    [
                        new(MessageBoxButtonFlags.EscapekeyDefault, 1, "Cancel"),
                        new(MessageBoxButtonFlags.ReturnkeyDefault, 0, "OK")
                    ],
                    default);

                if (!SDL.ShowMessageBox(in data, out var id))
                    throw new InvalidOperationException($"Cannot create message box: {SDL.GetError()}");

                if (id == 0) SDL.OpenURL(DiscordUrl);
            }
            catch (Exception e2)
            {
                w.Write(DateTimeOffset.Now + " - ");
                w.WriteLine(e2);
                w.WriteLine();
            }
            finally
            {
                if (show) Environment.FailFast(null, e);
                insideErrorHandler = false;
            }
        }
    }

    #endregion
}