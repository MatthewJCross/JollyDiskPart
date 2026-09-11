using System.Diagnostics;
using System.IO;
using System.Text;
using JollyDiskPart.Builders;
using JollyDiskPart.Models;

namespace JollyDiskPart.Services
{
    public class DiskPartService
    {
        private readonly string _diskPartExecutable = "diskpart";

        public async Task<DiskOperationResult> ExecuteBuilderAsync(DiskPartScriptBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            string script = builder.Build();

            return await ExecuteScriptAsync(script);
        }

        private async Task<DiskOperationResult> ExecuteScriptAsync(string script)
        {
            var totalSw = Stopwatch.StartNew(); 
            
            string scriptFile = Path.Combine(Path.GetTempPath(), $"diskpart_{Guid.NewGuid():N}.txt");
            DateTime started = DateTime.Now;

            try
            {
                await File.WriteAllTextAsync(scriptFile, script, Encoding.ASCII);

                ProcessStartInfo psi = new()
                {
                    FileName = _diskPartExecutable,
                    Arguments = $"/s \"{scriptFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new()
                {
                    StartInfo = psi
                };

                StringBuilder output = new();
                StringBuilder error = new();

                object outputLock = new();
                object errorLock = new();

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (outputLock)
                        {
                            output.AppendLine(e.Data);
                        }
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        lock (errorLock)
                        {
                            error.AppendLine(e.Data);
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                // Ensure all asynchronous stdout/stderr callbacks have completed.
                process.WaitForExit();

                string outputText = output.ToString();
                string errorText = error.ToString();

                // DiskPart normally writes failures to stdout
                string[] failureMessages =
                {
                    "DiskPart failed",
                    "DiskPart has encountered an error",
                    "Virtual Disk Service error",
                    "The disk is write protected",
                    "Access is denied",
                    "The selected volume has no letter or mount point",
                    "The selected partition is not valid",
                    "The operation is not supported",
                    "The parameter is incorrect",
                    "There is not enough usable space",
                    "The media is write protected",
                    "cannot",
                    "A device which does not exist",
                    "failed",
                    "No usable free extent could be found"
                };

                bool failed = failureMessages.Any(x => outputText.Contains(x, StringComparison.OrdinalIgnoreCase));
                if (failed && string.IsNullOrWhiteSpace(errorText))
                {
                    errorText = outputText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(line => failureMessages.Any(x => line.Contains(x, StringComparison.OrdinalIgnoreCase))) ?? outputText;
                }

                return new DiskOperationResult
                {
                    Success = process.ExitCode == 0 && !failed,
                    Output = outputText,
                    Error = errorText,
                    ExitCode = process.ExitCode,
                    Script = script,
                    Started = started,
                    Completed = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                return new DiskOperationResult
                {
                    Success = false,
                    Output = string.Empty,
                    Error = ex.Message,
                    ExitCode = -1,
                    Script = script,
                    Started = started,
                    Completed = DateTime.Now
                };
            }
            finally
            {
                try
                {
                    if (File.Exists(scriptFile))
                        File.Delete(scriptFile);
                }
                catch
                {
                }
            }
        }
    }
}
