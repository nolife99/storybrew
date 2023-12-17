using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BrewLib.Audio;
using BrewLib.Util;
using osuTK;
using osuTK.Graphics;
using StorybrewEditor.Processes;
using StorybrewEditor.Util;

namespace StorybrewEditor;

public static class Program
{
    public const string Name = "storybrew editor", Repository = "Damnae/storybrew", DiscordUrl = "https://discord.gg/0qfFOucX93QDNVN7";
    public readonly static Version Version = typeof(Editor).Assembly.GetName().Version;
    public readonly static string FullName = $"{Name} {Version} ({Repository})";

    public static AudioManager AudioManager { get; set; }
    public static Settings Settings { get; set; }

    static int mainThreadId;
    public static bool IsMainThread => Environment.CurrentManagedThreadId == mainThreadId;
    public static void CheckMainThread([CallerFilePath] string callerPath = "", [CallerLineNumber] int callerLine = -1, [CallerMemberName] string callerName = "")
    {
        if (IsMainThread) return;
        throw new InvalidOperationException($"{callerPath}:L{callerLine} {callerName} called from the thread '{Thread.CurrentThread.Name}', must be called from the main thread");
    }

    static void Main(string[] args)
    {
        mainThreadId = Environment.CurrentManagedThreadId;
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

            case "worker":
                if (args.Length < 2) return false;
                setupLogging(null, $"worker-{DateTime.UtcNow:yyyyMMddHHmmssfff}.log");
                SchedulingEnabled = true;
                ProcessWorker.Run(args[1]);
                return true;
        }
        return false;
    }

    #region Editor

    public static string Stats { get; private set; }

    static void startEditor()
    {
        SchedulingEnabled = true;
        Settings = new();

        NetHelper.Client = new();
        NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);

        Updater.NotifyEditorRun();

        var displayDevice = findDisplayDevice();

        using (var window = createWindow(displayDevice)) using (AudioManager = createAudioManager())
        using (Editor editor = new(window)) using (System.Drawing.Icon icon = new(typeof(Editor), "icon.ico"))
        {
            Trace.WriteLine($"{Environment.OSVersion} / Handle: 0x{Native.MainWindowHandle.ToString($"X{nint.Size}", CultureInfo.InvariantCulture)}");
            Trace.WriteLine($"graphics mode: {window.Context.GraphicsMode}");

            Native.SetWindowIcon(icon.Handle);
            window.Resize += (sender, e) =>
            {
                editor.Draw(1);
                window.SwapBuffers();
            };

            editor.Initialize();
            runMainLoop(window, editor,
                1000f / (Settings.UpdateRate > 0 ? Settings.UpdateRate : displayDevice.RefreshRate),
                1000f / (Settings.FrameRate > 0 ? Settings.FrameRate : displayDevice.RefreshRate));

            Settings.Save();
        }

        NetHelper.Client?.Dispose();
        foreach (IDisposable listener in Trace.Listeners) listener?.Dispose();
    }

    static DisplayDevice findDisplayDevice()
    {
        try
        {
            return DisplayDevice.GetDisplay(DisplayIndex.Default);
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Failed to use the default display device: {e}");

            var deviceIndex = 0;
            while (deviceIndex <= (int)DisplayIndex.Sixth) try
            {
                return DisplayDevice.GetDisplay((DisplayIndex)deviceIndex);
            }
            catch (Exception e2)
            {
                Trace.WriteLine($"Failed to use display device #{deviceIndex}: {e2}");
                ++deviceIndex;
            }
        }
        throw new InvalidOperationException("Failed to find a display device");
    }
    static GameWindow createWindow(DisplayDevice displayDevice)
    {
        var workArea = Screen.PrimaryScreen.WorkingArea;
        var ratio = displayDevice.Width / (float)displayDevice.Height;

        float windowWidth = 1360, windowHeight = windowWidth / ratio;
        if (windowHeight >= workArea.Height)
        {
            windowWidth = 1024;
            windowHeight = windowWidth / ratio;

            if (windowWidth >= workArea.Width)
            {
                windowWidth = 896;
                windowHeight = windowWidth / ratio;
            }
        }

        GameWindow window = new((int)windowWidth, (int)windowHeight, null, Name, GameWindowFlags.Default, displayDevice, 3, 0, GraphicsContextFlags.ForwardCompatible);
        Native.InitializeHandle(Name);
        Trace.WriteLine($"Window dpi scale: {window.Height / windowHeight}");

        window.Location = new(workArea.X + (workArea.Width - window.Size.Width) / 2, workArea.Y + (workArea.Height - window.Size.Height) / 2);
        if (window.Location.X < 0 || window.Location.Y < 0)
        {
            window.Location = workArea.Location;
            window.Size = workArea.Size;
            window.WindowState = WindowState.Maximized;
        }

        return window;
    }
    static AudioManager createAudioManager()
    {
        AudioManager audioManager = new(Native.MainWindowHandle)
        {
            Volume = Settings.Volume
        };
        Settings.Volume.OnValueChanged += (sender, e) => audioManager.Volume = Settings.Volume;

        return audioManager;
    }
    static void runMainLoop(GameWindow window, Editor editor, float fixedRateUpdate, float targetFrame)
    {
        float prev = 0, fixedRate = 0, av = 0, avActive = 0, longest = 0, lastStat = 0;
        var watch = Stopwatch.StartNew();

        while (window.Exists && !window.IsExiting)
        {
            var focused = window.Focused;
            var cur = (float)watch.Elapsed.TotalMilliseconds;
            var fixedUpdates = 0;

            window.ProcessEvents();
            while (cur - fixedRate >= fixedRateUpdate && fixedUpdates < 2)
            {
                fixedRate += fixedRateUpdate;
                ++fixedUpdates;

                editor.Update(fixedRate * 1E-3f);
            }
            if (focused && fixedUpdates == 0 && fixedRate < cur && cur < fixedRate + fixedRateUpdate) editor.Update(cur * 1E-3f, false);

            if (!window.Exists || window.IsExiting) return;
            if (window.WindowState != WindowState.Minimized)
            {
                editor.Draw(Math.Min((cur - fixedRate) / fixedRateUpdate, 1));
                window.SwapBuffers();
            }
            if (!window.Visible) window.Visible = true;

            RunScheduledTasks();
            window.VSync = focused ? VSyncMode.Off : VSyncMode.Adaptive;

            var active = (float)(watch.Elapsed.TotalMilliseconds - cur);
            if (window.WindowState != WindowState.Minimized)
            {
                var sleepTime = (focused ? targetFrame : fixedRateUpdate) - active;
                if (sleepTime > 0) Task.Delay((int)sleepTime).Wait();
            }

            Task.Run(() =>
            {
                var frameTime = cur - prev;
                prev = cur;

                av = (frameTime + av) * .5f;
                avActive = (active + avActive) * .5f;
                longest = Math.Max(frameTime, longest);

                if (lastStat + 150 < cur)
                {
                    Stats = $"fps:{1000 / av:0}/{1000 / avActive:0} (act:{avActive:0} avg:{av:0} hi:{longest:0})";

                    longest = 0;
                    lastStat = cur;
                }
                AudioManager.Update();
            });
        }
    }

    #endregion

    #region Scheduling

    public static bool SchedulingEnabled { get; private set; }
    static readonly ConcurrentQueue<Action> scheduledActions = new();

    public static void Schedule(Action action)
    {
        if (SchedulingEnabled) scheduledActions.Enqueue(action);
        else throw new InvalidOperationException("Scheduling isn't enabled!");
    }
    public static void Schedule(Action action, int delay) => Task.Run(async () =>
    {
        await Task.Delay(delay);
        Schedule(action);
    });
    public static void RunMainThread(Action action)
    {
        if (IsMainThread)
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
    public static void RunScheduledTasks()
    {
        if (scheduledActions.IsEmpty || !IsMainThread) return;

        while (scheduledActions.TryDequeue(out var action)) try
        {
            action();
        }
        catch (Exception e)
        {
            Trace.WriteLine($"Scheduled task {action.Method} failed:\n{e}");
        }
    }

    #endregion

    #region Error Handling

    public const string DefaultLogPath = "logs";

    static readonly object errorHandlerLock = new();
    static volatile bool insideErrorHandler;

    static void setupLogging(string logsPath = null, string commonLogFilename = null)
    {
        logsPath ??= DefaultLogPath;
        var tracePath = Path.Combine(logsPath, commonLogFilename ?? "trace.log");
        var exceptionPath = Path.Combine(logsPath, commonLogFilename ?? "exception.log");
        var crashPath = Path.Combine(logsPath, commonLogFilename ?? "crash.log");
        var freezePath = Path.Combine(logsPath, commonLogFilename ?? "freeze.log");

        if (!Directory.Exists(logsPath)) Directory.CreateDirectory(logsPath);
        else if (File.Exists(exceptionPath)) File.Delete(exceptionPath);

        Trace.Listeners.Add(new TextWriterTraceListener(File.CreateText(tracePath), Name));
        Trace.WriteLine($"{FullName}\n");

        AppDomain.CurrentDomain.FirstChanceException += (sender, e) => logError(e.Exception, exceptionPath, null, false);
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => logError((Exception)e.ExceptionObject, crashPath, "crash", true);

        Task.Run(async () =>
        {
            while (true)
            {
                Trace.Flush();
                await Task.Delay(10000);
            }
        });
    }
    static void logError(Exception e, string filename, string reportType, bool show)
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
                    w.Write(DateTime.Now + " - ");
                    w.WriteLine(e);
                    w.WriteLine();
                }

                if (reportType is not null) Report(reportType, e);
                if (show && MessageBox.Show($"An error occured:\n\n{e.Message} ({e.GetType().Name})\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.", FullName, MessageBoxButtons.OKCancel) is DialogResult.OK) 
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
    public static void Report(string type, Exception e) => NetHelper.BlockingPost("http://a-damnae.rhcloud.com/storybrew/report.php", new()
    {
        {"reporttype", type},
        {"source", Settings?.Id ?? "-"},
        {"version", Version.ToString()},
        {"content", e.ToString()}
    });

    #endregion
}