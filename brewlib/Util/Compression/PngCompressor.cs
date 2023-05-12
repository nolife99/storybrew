using BrewLib.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace BrewLib.Util.Compression
{
    public class PngCompressor : ImageCompressor
    {
        readonly static bool is64bit = Environment.Is64BitOperatingSystem;
        public PngCompressor(string utilityPath = null) : base(utilityPath) 
            => container = new AssemblyResourceContainer(Assembly.GetAssembly(typeof(PngCompressor)), "brewlib");

        protected override void compress(string path, string compressionType, 
            LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings, InputFormat inputFormat = null)
        {
            if (!File.Exists(path)) throw new ArgumentException(nameof(path));
            if (File.Exists(path) && string.IsNullOrEmpty(Path.GetExtension(path)) && string.IsNullOrEmpty(Convert.ToString(inputFormat)))
                throw new InvalidDataException("Input format is required for file without extension");

            try
            {
                UtilityName = is64bit ? compressionType != "lossy" ? "oxipng.exe" : "pngquant.exe" : "truepng.exe";
                ensureTool();

                var startInfo = new ProcessStartInfo(GetUtility(), appendArgs(path, compressionType, lossyInputSettings, losslessInputSettings))
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
                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error) && process.ExitCode != 0) throw new InvalidProgramException($"An error occured in the process: {error}");
                process.Close();
                process = null;
            }
            catch (Exception e)
            {
                ensureStop();
                throw new OperationCanceledException("Compression failed", e);
            }
        }
        protected override string appendArgs(string path, string compressionType, 
            LossyInputSettings lossyInputSettings, LosslessInputSettings losslessInputSettings)
        {
            var input = string.Format("\"{0}\"", path);
            var stringBuilder = new StringBuilder();

            if (is64bit)
            {
                if (compressionType == "lossy")
                {
                    stringBuilder.AppendFormat("{0} -o {0} -f --skip-if-larger --strip", input);
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
                    if (losslessInputSettings != null)
                    {
                        stringBuilder.AppendFormat(" -o {0} ", losslessInputSettings.OptimizationLevel);
                        stringBuilder.AppendFormat(" {0} ", losslessInputSettings.CustomInputArgs);
                    }
                    stringBuilder.AppendFormat("−s -a -Z --out {0} {0}", input);
                }
            }
            else
            {
                if (losslessInputSettings != null)
                {
                    stringBuilder.AppendFormat(" /o{0} ", losslessInputSettings.OptimizationLevel);
                    stringBuilder.AppendFormat(" {0} ", losslessInputSettings.CustomInputArgs);
                }
                stringBuilder.AppendFormat("/md remove all /quiet -Z /y /out {0} {0}", input);
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

            using (var stream = container.GetStream(UtilityName, ResourceSource.Embedded | ResourceSource.Relative)) 
            using (var dest = File.OpenWrite(path)) stream.CopyTo(dest);
        }
    }
}