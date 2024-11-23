namespace StorybrewEditor;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BrewLib.Audio;
using BrewLib.Util;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Util;
using WindowState = OpenTK.Windowing.Common.WindowState;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew",
        DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";

    public static readonly Version Version = typeof(Editor).Assembly.GetName().Version;
    public static readonly string FullName = $"{Name} {Version} ({Repository})";

    static int mainThreadId;

    public static AudioManager AudioManager { get; set; }
    public static Settings Settings { get; set; }

    [STAThread] static void Main(string[] args)
    {
        if (args.Length != 0 && handleArguments(args)) return;
        mainThreadId = Environment.CurrentManagedThreadId;

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

            case "worker":
                if (args.Length < 2) return false;
                setupLogging(null, $"worker-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.log");
                schedulingEnabled = true;
                return true;
        }

        return false;
    }

    #region Editor

    public static string Stats { get; private set; }

    static void startEditor()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        schedulingEnabled = true;
        Settings = new();

        Updater.NotifyEditorRun();

        var displayDevice = findDisplayDevice();
        using (var window = createWindow(displayDevice))
        {
            Trace.Write(Environment.OSVersion);
            Trace.Write(" / Handle: 0x");
            Trace.WriteLine(Native.MainWindowHandle.ToString($"X{nint.Size}", CultureInfo.InvariantCulture));

            using Editor editor = new(window);
            window.Resize += _ =>
            {
                editor.Draw(1);
                window.Context.SwapBuffers();
            };

            using (NetHelper.Client = new())
            {
                NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);
                editor.Initialize();

                using (AudioManager = createAudioManager())
                    runMainLoop(window, editor,
                        1000f / (Settings.UpdateRate > 0 ? Settings.UpdateRate : displayDevice.CurrentVideoMode.RefreshRate),
                        1000f / (Settings.FrameRate > 0 ? Settings.FrameRate : displayDevice.CurrentVideoMode.RefreshRate));
            }
        }

        Settings.Save();
    }

    static MonitorInfo findDisplayDevice()
    {
        try
        {
            return Monitors.GetPrimaryMonitor();
        }
        catch (Exception e)
        {
            Trace.TraceWarning($"Failed to use default display device: {e}");
            foreach (var monitor in Monitors.GetMonitors()) return monitor;
        }

        throw new InvalidOperationException("Failed to find a display device");
    }

    static unsafe NativeWindow createWindow(MonitorInfo displayDevice)
    {
        var workArea = displayDevice.WorkArea;
        var ratio = displayDevice.HorizontalResolution / (float)displayDevice.VerticalResolution;
        var dpiScale = displayDevice.VerticalScale;

        float windowWidth = 1360 * dpiScale, windowHeight = windowWidth / ratio;
        if (windowHeight >= workArea.Max.Y)
        {
            windowWidth = 1024 * dpiScale;
            windowHeight = windowWidth / ratio;

            if (windowWidth >= workArea.Max.X)
            {
                windowWidth = 896 * dpiScale;
                windowHeight = windowWidth / ratio;
            }
        }

        const ContextFlags debugContext =
#if DEBUG
            ContextFlags.Debug;
#else
            ContextFlags.Debug;
#endif

        if (debugContext is not ContextFlags.Debug) GLFW.WindowHint(WindowHintBool.ContextNoError, true);

        NativeWindow window = new(new()
        {
            Flags = debugContext,
            Profile = ContextProfile.Core,
            CurrentMonitor = displayDevice.Handle,
            APIVersion = new(3, 3),
            Title = Name,
            StartVisible = false
        });

        Native.InitializeHandle(Name, window.WindowPtr);
        Native.SetWindowIcon(typeof(Editor), "icon.ico");

        window.CenterWindow(new((int)windowWidth, (int)windowHeight));
        Trace.WriteLine($"Window dpi scale: {dpiScale}");

        if (window.Location is { X: >= 0, Y: >= 0 }) return window;

        window.ClientRectangle = workArea;
        window.WindowState = WindowState.Maximized;

        return window;
    }

    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new(Native.MainWindowHandle) { Volume = Settings.Volume };

        Settings.Volume.OnValueChanged += (_, _) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }

    static void runMainLoop(NativeWindow window, Editor editor, float fixedRateUpdate, float targetFrame)
    {
        float prev = 0, fixedRate = 0, av = 0, avActive = 0, longest = 0, lastStat = 0;
        var watch = Stopwatch.StartNew();

        while (window.Exists && !window.IsExiting)
        {
            var cur = watch.ElapsedMilliseconds;
            var focused = window.IsFocused;
            var fixedUpdates = 0;

            GLFW.PollEvents();
            AudioManager.Update();

            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates < 2)
            {
                fixedRate += fixedRateUpdate;
                ++fixedUpdates;

                editor.Update(fixedRate * .001f);
            }

            if (focused && fixedUpdates == 0 && fixedRate < cur && cur < fixedRate + fixedRateUpdate)
                editor.Update(cur * .001f, false);

            if (!window.Exists || window.IsExiting) return;

            editor.Draw(Math.Min((cur - fixedRate) / fixedRateUpdate, 1));
            window.Context.SwapBuffers();

            if (!window.IsVisible) window.IsVisible = true;
            while (scheduledActions.TryDequeue(out var action))
                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Scheduled task {action.Method}:\n{e}");
                }

            window.VSync = focused ? VSyncMode.Off : VSyncMode.On;

            var active = watch.ElapsedMilliseconds - cur;
            var sleepTime = (int)((focused ? targetFrame : fixedRateUpdate) - active);
            if (sleepTime > 0) Thread.Sleep(sleepTime);

            var frameTime = cur - prev;
            prev = cur;
            if (lastStat + 150 > cur) continue;

            av = (frameTime + av) * .5f;
            avActive = (active + avActive) * .5f;
            longest = Math.Max(frameTime, longest);

            Stats = $"fps:{1000 / av:0}/{1000 / avActive:0} (act:{avActive:0} avg:{av:0} hi:{longest:0})";

            longest = 0;
            lastStat = cur;
        }
    }

    #endregion

    #region Scheduling

    static bool schedulingEnabled;
    static readonly ConcurrentQueue<Action> scheduledActions = new();

    public static void Schedule(Action action)
    {
        if (schedulingEnabled) scheduledActions.Enqueue(action);
        else throw new InvalidOperationException("Scheduling isn't enabled!");
    }
    public static void Schedule(Action action, int delay) => Task.Delay(delay).ContinueWith(_ => Schedule(action));

    public static void RunMainThread(Action action)
    {
        if (Environment.CurrentManagedThreadId == mainThreadId)
        {
            action();
            return;
        }

        Exception ex = null;
        using (ManualResetEventSlim completed = new(false))
        {
            Schedule(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    ex = e;
                }

                completed.Set();
            });

            completed.Wait();
        }

        if (ex is not null) throw ex;
    }

    #endregion

    #region Error Handling

    const string DefaultLogPath = "logs";

    static readonly Lock errorHandlerLock = new();
    static volatile bool insideErrorHandler;

    static async void setupLogging(string logsPath = null, string commonLogFilename = null)
    {
        logsPath ??= DefaultLogPath;
        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");
        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");
        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else if (File.Exists(exceptionPath)) File.Delete(exceptionPath);

        using TextWriterTraceListener listener = new(File.CreateText(tracePath), Name);
        var domain = AppDomain.CurrentDomain;

        domain.FirstChanceException += (_, e) => logError(e.Exception, exceptionPath, false);
        domain.UnhandledException += (_, e) => logError((Exception)e.ExceptionObject, crashPath, true);

        Trace.Listeners.Add(listener);
        Trace.WriteLine($"{FullName}\n");

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false)) Trace.Flush();
    }

    static void logError(Exception e, string filename, bool show)
    {
        lock (errorHandlerLock)
        {
            if (insideErrorHandler) return;
            insideErrorHandler = true;

            try
            {
                var logPath = Path.Combine(Environment.CurrentDirectory, filename);
                using (StreamWriter w = new(logPath, true))
                {
                    w.Write(DateTimeOffset.Now + " - ");
                    w.WriteLine(e);
                    w.WriteLine();
                }

                if (show && MessageBox.Show($"An error occured:\n\n{e.Message} ({e.GetType().Name
                })\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.",
                    FullName, MessageBoxButton.OKCancel, MessageBoxImage.Error) is MessageBoxResult.OK)
                    NetHelper.OpenUrl(DiscordUrl);
            }
            catch (Exception e2)
            {
                Trace.WriteLine(e2.Message);
            }
            finally
            {
                insideErrorHandler = false;
            }
        }
    }

    public static void Report(string type, Exception e) => NetHelper.BlockingPost(
        "http://a-damnae.rhcloud.com/storybrew/report.php",
        new()
        {
            { "reporttype", type },
            { "source", Settings?.Id ?? "-" },
            { "version", Version.ToString() },
            { "content", e.ToString() }
        });

    #endregion
}