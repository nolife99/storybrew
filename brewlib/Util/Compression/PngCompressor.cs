using BrewLib.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace BrewLib.Util.Compression
{
    public class PngCompressor : ImageCompressor
    {
        readonly HashSet<string> files = new HashSet<string>();

        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(Assembly.GetAssembly(typeof(ImageCompressor)), "brewlib");

        protected override void compress(string path, string compressionType, 
            LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings, InputFormat inputFormat = null)
        {
            if (!Environment.Is64BitOperatingSystem && compressionType == "lossy")
            {
                Trace.TraceError("Lossy compression failed: Operating system is not x64. Falling back to lossless compress");
                compressionType = "";
            }
            if (!File.Exists(path)) throw new ArgumentException(nameof(path));
            if (File.Exists(path) && string.IsNullOrEmpty(Path.GetExtension(path)) && string.IsNullOrEmpty(Convert.ToString(inputFormat)))
                throw new InvalidDataException("Input format is required for file without extension");

            try
            {
                UtilityName = compressionType != "lossy" ? "optipng.exe" : "pngquant.exe";
                ensureTool();

                var startInfo = new ProcessStartInfo(GetUtility(), appendArgs(path, path + ".tmp", compressionType, lossyInputSettings, losslessInputSettings))
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

                process = process is null ? Process.Start(startInfo) : process;

                waitForExit();
                if (process.ExitCode != 0) throw new InvalidProgramException($"An error occured in the process: {process.StandardError.ReadToEnd()}");
                process.Close();
                process = null;

                var bytes = new FileInfo(path);
                var compressed = new FileInfo(path + ".tmp");
                if (bytes.Length > compressed.Length) File.Replace(path + ".tmp", path, null);
                else ClearDirectory();
            }
            catch (Exception e)
            {
                ensureStop();
                ClearDirectory();
                throw new OperationCanceledException("Compression failed", e);
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
                stringBuilder.AppendFormat("{0} -out {1}", input, output);
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
            if (process == null) return;
            if (!process.HasExited && !process.WaitForExit(ExecutionTimeout.HasValue ? (int)ExecutionTimeout.Value.TotalMilliseconds : int.MaxValue))
            {
                ensureStop();
                Trace.WriteLine("Image compression process exceeded execution timeout and was aborted");
            }
        }
        protected override void ensureTool()
        {
            var path = GetUtility();
            if (File.Exists(path)) return;

            using (var stream = container.GetStream(UtilityName, ResourceSource.Embedded | ResourceSource.Relative)) using (var dest = File.OpenWrite(path)) 
                stream.CopyTo(dest);
        }
        public void ClearDirectory()
        {
            foreach (var dir in files) foreach (var file in Directory.GetFiles(dir, "*.png.tmp")) File.Delete(file);
        }
    }
}