using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;

namespace StorybrewEditor.Processes
{
    public static class ProcessWorker
    {
        static bool exit;

        public static void Run(string identifier)
        {
            Trace.WriteLine($"channel: {identifier}");
            try
            {
                var name = $"sbrew-worker-{identifier}";
                using NamedPipeServerStream server = new(name);
                Trace.WriteLine($"{name}: ready");

                while (!exit)
                {
                    server.WaitForConnection();
                    Program.RunScheduledTasks();
                    server.Disconnect();
                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"ProcessWorker failed: {e}");
            }
        }
        public static void Exit()
        {
            Trace.WriteLine($"exiting");
            exit = true;
        }
    }
}