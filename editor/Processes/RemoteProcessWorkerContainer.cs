using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;

namespace StorybrewEditor.Processes
{
    public class RemoteProcessWorkerContainer : IDisposable
    {
        readonly NamedPipeServerStream pipeServer;

        public RemoteProcessWorker Worker { get; private set; }
        public RemoteProcessWorkerContainer()
        {
            pipeServer = new NamedPipeServerStream($"sbrew-{Guid.NewGuid()}");
            pipeServer.WaitForConnection();

            Worker = retrieveWorker(pipeServer);
        }

        static RemoteProcessWorker retrieveWorker(NamedPipeServerStream pipeServer)
        {
            while (true)
            {
                try
                {
                    Trace.WriteLine("Waiting for connection...");
                    pipeServer.WaitForConnection(); 
                    Trace.WriteLine("Connection established.");

                    var worker = (RemoteProcessWorker)JsonSerializer.Deserialize(pipeServer, typeof(RemoteProcessWorker));
                    Trace.WriteLine("Worker received.");

                    return worker;
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Couldn't start pipe: {e}");
                }

                Thread.Sleep(250);
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

                    pipeServer.Disconnect();
                    pipeServer.Dispose();
                }

                Worker = null;
                disposed = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}