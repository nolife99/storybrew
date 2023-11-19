using System;
using System.Diagnostics;
using System.IO;
using BrewLib.Data;

namespace BrewLib.Util.Compression
{
    public abstract class ImageCompressor(string utilityPath = null) : IDisposable
    {
        protected Process process;
        protected ResourceContainer container;

        public string UtilityPath { get; protected set; } = utilityPath ?? Path.GetDirectoryName(typeof(ImageCompressor).Assembly.Location) + "/cache/scripts";

        protected string utilName;
        public virtual string UtilityName
        {
            get => HashHelper.GetMd5(utilName + Environment.CurrentManagedThreadId);
            protected set => utilName = value;
        }

        public void LosslessCompress(string path) => compress(new Argument(path), false);
        public void Compress(string path) => compress(new Argument(path), true);
        public void LosslessCompress(string path, LosslessInputSettings settings) => compress(new Argument(path, settings), false);
        public void Compress(string path, LossyInputSettings settings) => compress(new Argument(path, null, settings), true);
        public void LosslessCompress(string path, LosslessInputSettings settings, InputFormat inputFormat) => compress(new Argument(path, settings, null, inputFormat), false);
        public void Compress(string path, LossyInputSettings settings, InputFormat inputFormat) => compress(new Argument(path, null, settings, inputFormat), true);

        protected abstract void compress(Argument arg, bool useLossy);
        protected abstract string appendArgs(string path, bool useLossy, LossyInputSettings lossy, LosslessInputSettings lossless);
        protected abstract void ensureTool();

        protected void ensureStop()
        {
            if (process is null) return;
            process.Close();
            process = null;
        }

        protected virtual string GetUtility() => Path.Combine(UtilityPath, UtilityName) + ".exe";

        protected bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing) ensureStop();
                container = null;
                disposed = true;
            }
        }

        public void Dispose() => Dispose(true);

        protected class Argument
        {
            internal readonly string path;
            internal readonly LosslessInputSettings lossless;
            internal readonly LossyInputSettings lossy;
            internal readonly InputFormat format;

            internal Argument(string path, LosslessInputSettings lossless = null, LossyInputSettings lossy = null, InputFormat format = null)
            {
                this.path = path;
                this.lossless = lossless;
                this.lossy = lossy;
                this.format = format;
            }
        }
    }
}