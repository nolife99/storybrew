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

        public string UtilityPath { get; set; }
        public string UtilityName { get; set; }

        internal TimeSpan? ExecutionTimeout { get; set; }
        internal UserCredential ProcessUser { get; set; }

        public ImageCompressor(string utilityPath = null)
            => UtilityPath = utilityPath ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/cache";

        public void LosslessCompress(string path) => compress(path, "", null, null);
        public void Compress(string path) => compress(path, "lossy", null, null);
        public void LosslessCompress(string path, LosslessInputSettings inputSettings) => compress(path, "", null, inputSettings);
        public void Compress(string path, LossyInputSettings inputSettings) => compress(path, "lossy", inputSettings, null);
        public void LosslessCompress(string path, LosslessInputSettings inputSettings, InputFormat inputFormat) => compress(path, "", null, inputSettings, inputFormat);
        public void Compress(string path, LossyInputSettings inputSettings, InputFormat inputFormat) => compress(path, "lossy", inputSettings, null, inputFormat);

        protected abstract void compress(string path, string compressionType, LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings, InputFormat inputFormat = null);
        protected abstract void waitForExit();
        protected abstract string appendArgs(string inputFile, string outputFile, string compressionType, LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings);
        protected abstract void ensureTool();

        protected void ensureStop()
        {
            if (process == null) return;
            if (process.HasExited) return;
            process.Kill();
            process = null;
        }

        internal string GetUtility() => Path.Combine(UtilityPath, UtilityName);
        internal void InitStartInfo(ProcessStartInfo startInfo)
        {
            if (ProcessUser == null) return;
            if (ProcessUser.Domain != null) startInfo.Domain = ProcessUser.Domain;
            if (ProcessUser.UserName != null) startInfo.UserName = ProcessUser.UserName;
            if (ProcessUser.Password == null) return;
            startInfo.Password = ProcessUser.Password;
        }
    }
}