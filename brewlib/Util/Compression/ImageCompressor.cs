using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BrewLib.Data;

namespace BrewLib.Util.Compression
{
    public abstract class ImageCompressor
    {
        protected Process process;
        protected ResourceContainer container;

        public string UtilityPath, UtilityName;

        internal TimeSpan? ExecutionTimeout;
        internal UserCredential ProcessUser;

        public ImageCompressor(string utilityPath = null)
            => UtilityPath = utilityPath ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/cache";

        public void LosslessCompress(string path) => doCompress(path, "", null, null);
        public void Compress(string path) => doCompress(path, "lossy", null, null);
        public void LosslessCompress(string path, LosslessInputSettings settings) => doCompress(path, "", null, settings);
        public void Compress(string path, LossyInputSettings settings) => doCompress(path, "lossy", settings, null);
        public void LosslessCompress(string path, LosslessInputSettings settings, InputFormat inputFormat) => doCompress(path, "", null, settings, inputFormat);
        public void Compress(string path, LossyInputSettings settings, InputFormat inputFormat) => doCompress(path, "lossy", settings, null, inputFormat);

        protected abstract void doCompress(string path, string type, LossyInputSettings lossy, LosslessInputSettings lossless, InputFormat inputFormat = null);
        protected abstract string appendArgs(string path, string type, LossyInputSettings lossy, LosslessInputSettings lossless);
        protected abstract void ensureTool();

        protected void ensureStop()
        {
            if (process is null) return;
            process.Close();
            process = null;
        }

        internal string GetUtility() => Path.Combine(UtilityPath, UtilityName);
    }
}