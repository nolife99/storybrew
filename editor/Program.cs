namespace StorybrewEditor;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BrewLib.Audio;
using BrewLib.Util;
using ManagedBass;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Vector = System.Numerics.Vector;

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
        Settings = new();
        Updater.NotifyEditorRun();
        Native.MainThreadScheduler = Schedule;

        var audioCreateTask = Task.Run(createAudioManager);

        var displayDevice = Monitors.GetPrimaryMonitor();
        using (var window = createWindow(displayDevice))
        {
            using Editor editor = new(window);
            using (NetHelper.Client = new())
            {
                NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
                editor.Initialize(displayDevice);

                var iconSetTask = Native.SetWindowIcon(editor.ResourceContainer, "icon.ico");
                using (AudioManager = audioCreateTask.Result)
                {
                    window.Move += _ => refresh();
                    window.Resize += _ => refresh();

                    runMainLoop(window,
                        editor,
                        TimeSpan.TicksPerSecond / (Settings.UpdateRate > 0 ?
                            Settings.UpdateRate :
                            displayDevice.CurrentVideoMode.RefreshRate),
                        TimeSpan.TicksPerSecond / (Settings.FrameRate > 0 ?
                            Settings.FrameRate :
                            displayDevice.CurrentVideoMode.RefreshRate));

                    void refresh() => Bass.UpdateThreads = 1;
                }

                iconSetTask.Wait();
            }
        }

        Settings.Save();
    }

    static NativeWindow createWindow(MonitorInfo displayDevice)
    {
        const ContextFlags debugContext =
#if DEBUG
            ContextFlags.Debug | ContextFlags.ForwardCompatible;
#else
            ContextFlags.ForwardCompatible;

        GLFW.WindowHint(WindowHintBool.ContextNoError, true);
#endif

        NativeWindow window = new(new()
        {
            Flags = debugContext,
            CurrentMonitor = displayDevice.Handle,
            Title = Name,
            StartVisible = false,
            StartFocused = false,
            DepthBits = 0,
            StencilBits = 0,
            AutoLoadBindings = false
        });

        GL.LoadBindings(new GLFWBindingsContext());
        Native.InitializeHandle(window);

        if (Vector.IsHardwareAccelerated) Trace.WriteLine($"SIMD Vector Alignment: {Vector<byte>.Count} bytes");

        return window;
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new() { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(NativeWindow window, Editor editor, long fixedRateUpdate, long targetFrame)
    {
        long prev = Stopwatch.GetTimestamp(), fixedRate = 0, av = 0, avActive = 0, longest = 0, lastStat = 0,
            statsUpdate = targetFrame * 5;

        var windowContext = window.Context;

        var exiting = false;
        window.Closing += _ => exiting = true;

        window.IsVisible = true;
        window.Focus();

        while (!exiting)
        {
            var cur = Stopwatch.GetTimestamp();
            var fixedUpdates = 0;

            GLFW.PollEvents();
            AudioManager.Update(targetFrame);

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates++ < 2)
            {
                fixedRate += fixedRateUpdate;
                editor.Update(fixedRate / (float)TimeSpan.TicksPerSecond);
            }

            if (window.IsFocused && fixedUpdates == 0 && fixedRate < cur && cur < fixedRate + fixedRateUpdate)
                editor.Update(cur / (float)TimeSpan.TicksPerSecond, false);

            var draws = editor.Draw();
            windowContext.SwapBuffers();

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
            var sleepTime = (window.IsFocused ? targetFrame : fixedRateUpdate) - active;

            if (sleepTime > 0)
            {
                Native.AccurateSleep(sleepTime);
                Bass.UpdateThreads = 0;
            }
            else Bass.UpdateThreads = 1;

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + statsUpdate > cur) continue;

            av = (frameTime + av) / 2;
            avActive = (active + avActive) / 2;
            longest = long.Max(frameTime, longest);

            buildStatsMessage(editor, av, avActive, longest, draws);

            lastStat = cur;
        }
    }

    static void buildStatsMessage(Editor editor, long av, long avActive, long longest, int draws)
    {
        if (!editor.statsLabel.Visible) return;

        const long ticks = TimeSpan.TicksPerSecond;
        const float millis = TimeSpan.TicksPerMillisecond;

        using var result = StringHelper.Interpolate(CultureInfo.InvariantCulture,
            $"{ticks / av}/{ticks / avActive}fps (act:{avActive / millis:f2} avg:{av / millis:f2} hi:{longest / millis:f2})\n{draws} draws");

        editor.statsLabel.Text = result.AsReadOnlySpan();
    }

    #endregion

    #region Scheduling

    static readonly Lock schedulerLock = new();
    static readonly List<IDisposable> scheduledActions = [];

    public static ValueTask Schedule<TState>(Action<TState> action, TState state = default)
    {
        if (GLFWProvider.IsOnMainThread)
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
        logsPath ??= DefaultLogPath;
        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");

        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");

        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else if (File.Exists(exceptionPath)) File.Delete(exceptionPath);

        TextWriterTraceListener listener = new(File.CreateText(tracePath), Name);

        var domain = AppDomain.CurrentDomain;
        domain.FirstChanceException += (_, e) => logError(e.Exception, exceptionPath, false);
        domain.UnhandledException += (_, e) => logError((Exception)e.ExceptionObject, crashPath, e.IsTerminating);

        Trace.Listeners.Add(listener);
        Trace.WriteLine(FullName);

        Timer timer = new(s => ((TraceListener)s)!.Flush(), listener, 5000, 1000);

        domain.ProcessExit += (_, _) =>
        {
            timer.Dispose();
            listener.Dispose();
        };
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

                var result = MessageBox.Show(
                    $"An error occurred:\n\n{e.Message} ({e.GetType().Name})\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.",
                    FullName,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Error);

                if (result is MessageBoxResult.OK)
                    Process.Start(new ProcessStartInfo { FileName = DiscordUrl, UseShellExecute = true });
            }
            catch (Exception e2)
            {
                w.Write(DateTimeOffset.Now + " - ");
                w.WriteLine(e2);
                w.WriteLine();
            }
            finally
            {
                insideErrorHandler = false;
            }
        }
    }

    #endregion
}