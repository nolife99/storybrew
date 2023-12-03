using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading.Tasks;

namespace StorybrewEditor.Processes;

public class RemoteProcessWorkerContainer : IDisposable
{
    NamedPipeServerStream server;
    Process process;

    public RemoteProcessWorker Worker { get; private set; }

    public RemoteProcessWorkerContainer()
    {
        var identifier = Guid.NewGuid().ToString();
        var workerUrl = $"sbrew-worker-{identifier}";

        server = new(workerUrl);
        startProcess(identifier);
        Worker = retrieveWorker(workerUrl);
    }

    private void startProcess(string identifier)
    {
        var executablePath = Assembly.GetExecutingAssembly().Location;
        var workingDirectory = Path.GetDirectoryName(executablePath);
        process = new()
        {
            StartInfo = new(executablePath, $"worker \"{identifier}\"")
            {
                WorkingDirectory = workingDirectory
            }
        };
        process.Start();
    }

    RemoteProcessWorker retrieveWorker(string workerUrl)
    {
        while (true)
        {
            using (var delay = Task.Delay(250)) delay.Wait();
            try
            {
                Trace.WriteLine($"Retrieving {workerUrl}");
                var worker = (RemoteProcessWorker)Activator.CreateInstance(typeof(RemoteProcessWorker));
                return worker;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Couldn't start ipc: {e}");
            }
        }
    }

    #region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                try
                {
                    Worker.Dispose();
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Failed to dispose the worker: {e}");
                }

                if (!process.WaitForExit(3000)) process.Kill();
                server.Close();
            }

            Worker = null;

            process.Close();
            process = null;

            server = null;
            disposed = true;
        }
    }

    public void Dispose() => Dispose(true);

    #endregion
}