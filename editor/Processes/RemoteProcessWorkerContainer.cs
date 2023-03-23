using System;
using System.Diagnostics;
using System.IO.Pipes;
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

        RemoteProcessWorker retrieveWorker(NamedPipeServerStream pipeServer)
        {
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            var wait = new ManualResetEventSlim();

            while (true)
            {
                try
                {
                    Trace.WriteLine("Waiting for connection...");
                    pipeServer.WaitForConnection();
                    Trace.WriteLine("Connection established.");

                    var worker = (RemoteProcessWorker)formatter.Deserialize(pipeServer);
                    Trace.WriteLine("Worker received.");

                    return worker;
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Couldn't start pipe: {e}");
                }

                wait.Wait(250);
            }
        }

        #region IDisposable Support

        bool disposed = false;
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