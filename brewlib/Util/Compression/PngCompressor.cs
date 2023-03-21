using BrewLib.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace BrewLib.Util.Compression
{
    public class PngCompressor : ImageCompressor, IDisposable
    {
        bool disposedValue;
        readonly HashSet<string> directories = new HashSet<string>();

        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(Assembly.GetAssembly(typeof(ImageCompressor)), "brewlib");

        protected override void compress(string path, string compressionType, 
            LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings, InputFormat inputFormat = null)
        {
            try
            {
                if (!File.Exists(path)) throw new ArgumentException(nameof(path));
                if (File.Exists(path) && string.IsNullOrEmpty(Path.GetExtension(path)) && string.IsNullOrEmpty(Convert.ToString(inputFormat)))
                    throw new InvalidDataException("Input format is required for file without extension");
                    
                var bytes = File.ReadAllBytes(path);
                if (File.Exists(path + "_")) throw new IOException($"Compression failed: {path + "_"} exists");

                UtilityName = compressionType != "lossy" ? "optipng.exe" : "pngquant.exe";
                ensureTool();

                var startInfo = new ProcessStartInfo(GetUtility(), appendArgs(path, path + "_", compressionType, lossyInputSettings, losslessInputSettings))
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(UtilityPath),
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                InitStartInfo(startInfo);

                process = process is null ? Process.Start(startInfo) : throw new InvalidOperationException("Compression has already started");

                waitForExit();
                if (process.ExitCode != 0) throw new InvalidProgramException("Some error occured");
                process.Close();
                process = null;

                var compressed = File.ReadAllBytes(path + "_");
                if (bytes.Length > compressed.Length) File.WriteAllBytes(path, compressed);
                directories.Add(Path.GetDirectoryName(path));
            }
            catch (Exception e)
            {
                ensureStop();
                Trace.WriteLine($"Compression failed: {e}");
                throw;
            }
        }
        protected override string appendArgs(string inputFile, string outputFile, string compressionType, 
            LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings)
        {
            var input = string.Format("\"{0}\"", inputFile);
            var output = string.Format("\"{0}\"", outputFile);
            var stringBuilder = new StringBuilder();

            if (compressionType == "lossy")
            {
                stringBuilder.AppendFormat("{0} -o {1} ", input, output);
                if (lossyInputSettings != null)
                {
                    if (lossyInputSettings.MinQuality >= 0 && lossyInputSettings.MaxQuality > 0 && lossyInputSettings.MaxQuality <= 100)
                        stringBuilder.AppendFormat(" --quality {0}-{1} ", lossyInputSettings.MinQuality, lossyInputSettings.MaxQuality);

                    if (lossyInputSettings.Speed > 0 && lossyInputSettings.Speed <= 10)
                        stringBuilder.AppendFormat(" -s{0} ", lossyInputSettings.Speed);

                    stringBuilder.AppendFormat(" {0} ", lossyInputSettings.CustomInputArgs);
                }
            }
            else
            {
                stringBuilder.AppendFormat("{0} -out {1} ", input, output);
                if (losslessInputSettings != null)
                {
                    stringBuilder.AppendFormat(" {0} ", losslessInputSettings.OptimizationLevel);
                    stringBuilder.AppendFormat(" {0} ", losslessInputSettings.CustomInputArgs);
                }
            }
            return stringBuilder.ToString();
        }
        protected override void waitForExit()
        {
            if (process == null) throw new Exception("Image compression process was aborted");
            if (!process.HasExited && !process.WaitForExit(ExecutionTimeout.HasValue ? (int)ExecutionTimeout.Value.TotalMilliseconds : int.MaxValue))
            {
                ensureStop();
                throw new TimeoutException("Image compression process exceeded execution timeout and was aborted");
            }
        }
        protected override void ensureTool()
        {
            var path = GetUtility();
            if (File.Exists(path)) return;

            using (var stream = container.GetStream(UtilityName, ResourceSource.Embedded | ResourceSource.Relative))
            using (var dest = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) stream.CopyTo(dest);
        }
        public void Dispose()
        {
            if (!disposedValue)
            {
                foreach (var dir in directories) foreach (var file in Directory.GetFiles(dir, "*.png_")) File.Delete(file);
                disposedValue = true;
            }
        }
    }
}