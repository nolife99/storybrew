using BrewLib.Audio;
using BrewLib.Util;
using osuTK;
using osuTK.Graphics;
using StorybrewEditor.Processes;
using StorybrewEditor.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StorybrewEditor
{
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

        [STAThread] static void Main(string[] args)
        {
            mainThreadId = Environment.CurrentManagedThreadId;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.SystemDefault;

            NetHelper.Client = new System.Net.Http.HttpClient();
            NetHelper.Client.DefaultRequestHeaders.Add("user-agent", Name);

            if (args.Length != 0 && handleArguments(args)) return;

            setupLogging();
            startEditor();

            NetHelper.Client?.Dispose();
            foreach (TraceListener listener in Trace.Listeners) listener.Dispose();
        }
        static bool handleArguments(string[] args)
        {
            switch (args[0])
            {
                case "update":
                    if (args.Length < 3) return false;
                    setupLogging(Path.Combine(args[1], DefaultLogPath), "update.log");
                    Updater.Update(args[1], new Version(args[2]));
                    return true;

                case "build":
                    setupLogging(null, "build.log");
                    Builder.Build();
                    return true;

                case "worker":
                    if (args.Length < 2) return false;
                    setupLogging(null, $"worker-{DateTime.UtcNow:yyyyMMddHHmmssfff}.log");
                    enableScheduling();
                    ProcessWorker.Run(args[1]);
                    return true;
            }
            return false;
        }

        #region Editor

        public static string Stats { get; set; }

        static void startEditor()
        {
            enableScheduling();

            Settings = new();
            Updater.NotifyEditorRun();

            var displayDevice = findDisplayDevice();

            using (var window = createWindow(displayDevice)) using (AudioManager = createAudioManager())
            using (Editor editor = new(window)) using (System.Drawing.Icon icon = new(typeof(Editor), "icon.ico"))
            {
                Trace.WriteLine($"{Environment.OSVersion} / {window.WindowInfo}");
                Trace.WriteLine($"graphics mode: {window.Context.GraphicsMode}");

                Native.SetWindowIcon(icon.Handle);
                window.Resize += (sender, e) =>
                {
                    editor.Draw(1);
                    window.SwapBuffers();
                };

                editor.Initialize();

                runMainLoop(window, editor, 
                    1d / (Settings.UpdateRate > 0 ? Settings.UpdateRate : displayDevice.RefreshRate), 
                    1d / (Settings.FrameRate > 0 ? Settings.FrameRate : displayDevice.RefreshRate));

                Settings.Save();
            }
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
            var primaryScreenArea = Screen.PrimaryScreen.WorkingArea;

            int windowWidth = 1366, windowHeight = 768;
            if (windowHeight >= primaryScreenArea.Height)
            {
                windowWidth = 1024;
                windowHeight = 600;
                if (windowWidth >= primaryScreenArea.Width) windowWidth = 800;
            }
            var window = new GameWindow(windowWidth, windowHeight, null, Name, GameWindowFlags.Default, displayDevice, 2, 0, GraphicsContextFlags.ForwardCompatible);
            Trace.WriteLine($"Window dpi scale: {window.Height / (float)windowHeight}");

            window.Location = new Point(
                (int)(primaryScreenArea.Left + (primaryScreenArea.Width - window.Size.Width) * .5f),
                (int)(primaryScreenArea.Top + (primaryScreenArea.Height - window.Size.Height) * .5f)
            );
            if (window.Location.X < 0 || window.Location.Y < 0)
            {
                window.Location = primaryScreenArea.Location;
                window.Size = primaryScreenArea.Size;
                window.WindowState = WindowState.Maximized;
            }
            window.InitializeHandle();

            return window;
        }
        static AudioManager createAudioManager()
        {
            var audioManager = new AudioManager(Native.MainWindowHandle)
            {
                Volume = Settings.Volume
            };
            Settings.Volume.OnValueChanged += (sender, e) => audioManager.Volume = Settings.Volume;

            return audioManager;
        }
        static void runMainLoop(GameWindow window, Editor editor, double fixedRateUpdateDuration, double targetFrameDuration)
        {
            double previousTime = 0, fixedRateTime = 0, averageFrame = 0, averageActive = 0, longestFrame = 0, lastStatTime = 0;
            var windowDisplayed = false;
            var watch = Stopwatch.StartNew();

            window.VSync = VSyncMode.Adaptive;
            using var reset = new ManualResetEventSlim(); while (window.Exists && !window.IsExiting)
            {
                var focused = window.Focused;
                var currentTime = watch.Elapsed.TotalSeconds;
                var fixedUpdates = 0;

                AudioManager.Update();
                window.ProcessEvents();

                while (currentTime - fixedRateTime >= fixedRateUpdateDuration && fixedUpdates < 2)
                {
                    fixedRateTime += fixedRateUpdateDuration;
                    ++fixedUpdates;

                    editor.Update(fixedRateTime, true);
                }
                if (focused && fixedUpdates == 0 && fixedRateTime < currentTime && currentTime < fixedRateTime + fixedRateUpdateDuration) editor.Update(currentTime, false);

                if (!window.Exists || window.IsExiting) return;
                if (window.WindowState != WindowState.Minimized)
                {
                    var tween = Math.Min((currentTime - fixedRateTime) / fixedRateUpdateDuration, 1);
                    editor.Draw(tween);
                    window.SwapBuffers();
                }

                if (!windowDisplayed)
                {
                    window.Visible = true;
                    windowDisplayed = true;
                }

                RunScheduledTasks();

                var activeDuration = watch.Elapsed.TotalSeconds - currentTime;
                if (window.VSync is VSyncMode.Off && window.WindowState != WindowState.Minimized)
                {
                    var sleepTime = (focused ? targetFrameDuration : fixedRateUpdateDuration) - activeDuration;
                    if (sleepTime > 0) reset.WaitHandle.WaitOne(TimeSpan.FromSeconds(sleepTime));
                }

                var frameTime = currentTime - previousTime;
                previousTime = currentTime;

                averageFrame = (frameTime + averageFrame) / 2;
                averageActive = (activeDuration + averageActive) / 2;
                longestFrame = Math.Max(frameTime, longestFrame);

                if (lastStatTime + .2 < currentTime)
                {
                    Stats = $"fps:{1 / averageFrame:f}/{1 / averageActive:f} (act:{averageActive * 1000:f} avg:{averageFrame * 1000:f} hi:{longestFrame * 1000:f})";

                    longestFrame = 0;
                    lastStatTime = currentTime;
                }
            }
        }

        #endregion

        #region Scheduling

        public static bool SchedulingEnabled { get; set; }
        static readonly ConcurrentQueue<Action> scheduledActions = new();

        public static void enableScheduling() => SchedulingEnabled = true;

        /// <summary>
        /// Schedule the action to run in the main thread.
        /// Exceptions will be logged.
        /// </summary>
        public static void Schedule(Action action)
        {
            if (SchedulingEnabled) scheduledActions.Enqueue(action);
            else throw new InvalidOperationException("Scheduling isn't enabled!");
        }

        /// <summary>
        /// Schedule the action to run in the main thread after a delay (in milliseconds).
        /// Exceptions will be logged.
        /// </summary>
        public static void Schedule(Action action, int delay) => Task.Run(async () =>
        {
            await Task.Delay(delay);
            Schedule(action);
        });

        /// <summary>
        /// Run the action synchronously in the main thread.
        /// Exceptions will be thrown to the calling thread.
        /// </summary>
        public static void RunMainThread(Action action)
        {
            if (IsMainThread)
            {
                action();
                return;
            }

            using ManualResetEvent completed = new(false);
            Exception exception = null;
            Schedule(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    exception = e;
                }
                completed.Set();
            });
            completed.WaitOne();

            if (exception != null) throw exception;
        }
        public static void RunScheduledTasks()
        {
            CheckMainThread();

            Action action = null;
            for (var i = 0; i < scheduledActions.Count; ++i) try
            {
                if (scheduledActions.TryDequeue(out action)) action();
                else throw new InvalidOperationException("Retrieving task failed");
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

        static void setupLogging(string logsPath = null, string commonLogFilename = null, bool checkFrozen = false)
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

            if (checkFrozen) setupFreezeCheck(e => logError(e, freezePath, null, false));

            Trace.AutoFlush = true;
        }
        static void logError(Exception e, string filename, string reportType, bool show)
        {
            lock (errorHandlerLock)
            {
                if (insideErrorHandler) return;
                insideErrorHandler = true;

                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
                    using (var w = new StreamWriter(logPath, true))
                    {
                        w.Write(DateTime.Now + " - ");
                        w.WriteLine(e);
                        w.WriteLine();
                    }

                    if (reportType != null) Report(reportType, e);
                    if (show)
                    {
                        var result = MessageBox.Show($"An error occured:\n\n{e.Message} ({e.GetType().Name})\n\nClick Ok if you want to receive and invitation to a Discord server where you can get help with this problem.", FullName, MessageBoxButtons.OKCancel);
                        if (result == DialogResult.OK) NetHelper.OpenUrl(DiscordUrl);
                    }
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
        public static void Report(string type, Exception e) => NetHelper.BlockingPost("http://a-damnae.rhcloud.com/storybrew/report.php", new Dictionary<string, string>
        {
            {"reporttype", type},
            {"source", Settings?.Id ?? "-"},
            {"version", Version.ToString()},
            {"content", e.ToString()}
        });

        static void setupFreezeCheck(Action<Exception> action)
        {
            var mainThread = Thread.CurrentThread;

            var cancel = new CancellationTokenSource();
            var thread = new Thread(() =>
            {
                var answered = false;
                var frozen = 0;
                var cancellationToken = cancel.Token;

                while (!SchedulingEnabled) Thread.Sleep(1000);

                while (!cancellationToken.IsCancellationRequested)
                {
                    answered = false;
                    Schedule(() => answered = true);

                    Thread.Sleep(1000);

                    if (!answered) frozen++;

                    if (frozen >= 3)
                    {
                        frozen = 0;
                        cancel.Cancel();

                        try
                        {
                            var trace = new StackTrace(true);
                            action(new ThreadStateException(trace.ToString()));
                        }
                        catch (ThreadStateException e)
                        {
                            action(e);
                        }
                    }
                }

                cancel.Dispose();
            })
            { Name = "Freeze Checker", IsBackground = true };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        #endregion
    }
}