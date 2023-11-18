using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BrewLib.Data;

namespace BrewLib.Util.Compression
{
    public abstract class ImageCompressor(string utilityPath = null) : IDisposable
    {
        public IEnumerable<string> Files => toCompress.Select(s => s.path).Concat(lossyCompress.Select(s => s.path));
        protected List<Argument> toCompress = [], lossyCompress = [];
        protected List<string> toCleanup = [];

        protected Process process;
        protected ResourceContainer container;

        public string UtilityPath { get; protected set; } = utilityPath ?? Path.GetDirectoryName(typeof(ImageCompressor).Assembly.Location) + "/cache/scripts";

        protected string utilName;
        public string UtilityName
        {
            get => HashHelper.GetMd5(utilName + Environment.CurrentManagedThreadId);
            protected set => utilName = value;
        }

        public void LosslessCompress(string path) => toCompress.Add(new Argument(path));
        public void Compress(string path) => lossyCompress.Add(new Argument(path));
        public void LosslessCompress(string path, LosslessInputSettings settings) => toCompress.Add(new Argument(path, settings));
        public void Compress(string path, LossyInputSettings settings) => lossyCompress.Add(new Argument(path, null, settings));
        public void LosslessCompress(string path, LosslessInputSettings settings, InputFormat inputFormat) => toCompress.Add(new Argument(path, settings, null, inputFormat));
        public void Compress(string path, LossyInputSettings settings, InputFormat inputFormat) => lossyCompress.Add(new Argument(path, null, settings, inputFormat));

        protected abstract Task doCompress();
        protected abstract string appendArgs(string path, bool useLossy, LossyInputSettings lossy, LosslessInputSettings lossless);
        protected abstract void ensureTool();

        protected void ensureStop()
        {
            if (process is null) return;
            process.Close();
            process = null;
        }

        internal string GetUtility() => Path.Combine(UtilityPath, UtilityName) + ".exe";

        protected bool disposed;
        protected virtual async void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (toCompress.Count > 0 && lossyCompress.Count > 0) try
                {
                    await doCompress();
                }
                finally
                {
                    if (disposing) ensureStop();
                    for (var i = 0; i < toCleanup.Count; ++i) if (File.Exists(toCleanup[i])) File.Delete(toCleanup[i]);

                    toCleanup.Clear();
                    toCompress.Clear();
                    lossyCompress.Clear();
                    toCleanup = null;
                    toCompress = null;
                    lossyCompress = null;

                    container = null;
                }
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