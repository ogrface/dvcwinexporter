using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CrmSolutionExporter
{
    public record SolutionInfo(string UniqueName, string FriendlyName, string Version);

    public static class DataverseActions
    {
        public static bool ValidateServerUrl(string serverUrl)
        {
            var pattern = @"([\w-]+)\.crm([0-9]*)\.(microsoftdynamics|dynamics|crm[\w-]*)\.(com|de|us|cn)";
            return System.Text.RegularExpressions.Regex.IsMatch(serverUrl, pattern);
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static async Task<List<SolutionInfo>> GetUnmanagedSolutions(Action<string> log)
        {
            log("Retrieving solutions via PAC CLI...");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pac",
                    Arguments = "solution list",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    Environment = { ["NO_COLOR"] = "1" }
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"Failed to list solutions (exit code {process.ExitCode}): {error}");

            return ParseSolutionList(output);
        }

        private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

        private static string StripAnsiAndControlChars(string input)
        {
            var cleaned = AnsiEscapeRegex.Replace(input, "");
            return new string(cleaned.Where(c => !char.IsControl(c) || c == '\r' || c == '\n' || c == '\t').ToArray());
        }

        private static List<SolutionInfo> ParseSolutionList(string output)
        {
            output = StripAnsiAndControlChars(output);
            var solutions = new List<SolutionInfo>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.TrimEnd())
                               .ToList();

            // Find the header line containing column names
            int headerIdx = lines.FindIndex(l => l.Contains("Unique Name") && l.Contains("Managed"));
            if (headerIdx < 0 || headerIdx + 1 >= lines.Count) return solutions;

            var headerLine = lines[headerIdx];

            // Determine column start positions from header keywords
            int colUniqueName = headerLine.IndexOf("Unique Name", StringComparison.OrdinalIgnoreCase);
            int colFriendlyName = headerLine.IndexOf("Friendly Name", StringComparison.OrdinalIgnoreCase);
            int colVersion = headerLine.IndexOf("Version", StringComparison.OrdinalIgnoreCase);
            int colManaged = headerLine.IndexOf("Managed", StringComparison.OrdinalIgnoreCase);

            if (colUniqueName < 0 || colFriendlyName < 0 || colVersion < 0 || colManaged < 0)
                return solutions;

            // Parse data lines after header
            for (int i = headerIdx + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                // Skip separator lines if present
                if (line.Contains('\u2500') || line.All(c => c == '-' || c == ' ')) continue;

                string Extract(int start, int end) =>
                    start >= line.Length ? "" : line[start..Math.Min(end, line.Length)].Trim();

                var uniqueName = Extract(colUniqueName, colFriendlyName);
                var friendlyName = Extract(colFriendlyName, colVersion);
                var version = Extract(colVersion, colManaged);
                var managed = colManaged < line.Length ? line[colManaged..].Trim() : "";

                if (managed.Equals("False", StringComparison.OrdinalIgnoreCase))
                {
                    solutions.Add(new SolutionInfo(uniqueName, friendlyName, version));
                }
            }

            solutions.Sort((a, b) => string.Compare(a.UniqueName, b.UniqueName, StringComparison.OrdinalIgnoreCase));
            return solutions;
        }

        public static async Task AuthenticatePacCli(string serverUrl, Action<string> log)
        {
            if (!serverUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                serverUrl = "https://" + serverUrl;
            }

            log($"Authenticating PAC CLI to {serverUrl}...");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pac",
                    Arguments = $"auth create --environment \"{serverUrl}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(output))
                log(output);
            if (!string.IsNullOrEmpty(error))
                log(error);

            if (process.ExitCode != 0)
                throw new Exception($"PAC CLI authentication failed (exit code {process.ExitCode}).");

            log("PAC CLI authenticated successfully.");
        }

        public static void CheckDotNetInstallation(Action<string> log)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "--list-sdks",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var sdks = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                log("Installed .NET SDKs:");
                log(sdks);
            }
            catch (Exception)
            {
                log("The 'dotnet' command is not available. Please ensure .NET is installed.");
            }
        }

        public static async Task EnsurePacCliInstalled(Action<string> log)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "pac",
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception("PAC CLI not found");
                }

                log("PAC CLI is already installed.");
            }
            catch
            {
                log("Installing PAC CLI...");
                var installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "tool install --global Microsoft.PowerApps.CLI.Tool",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                installProcess.Start();
                await installProcess.WaitForExitAsync();
                log("PAC CLI installed successfully.");
            }
        }

        public static async Task ExportSolution(string solutionName,
            string solutionFilePath, bool managed, Action<string> log)
        {
            var suffix = managed ? "_managed" : "";
            var fileName = $"{solutionName}{suffix}.zip";
            var fullPath = Path.Combine(solutionFilePath, fileName);

            var managedFlag = managed ? " --managed" : "";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pac",
                    Arguments = $"solution export --name \"{solutionName}\" --path \"{fullPath}\"{managedFlag} --overwrite",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(output))
                log(output);
            if (!string.IsNullOrEmpty(error))
                log(error);

            if (process.ExitCode != 0)
                throw new Exception($"Failed to export solution '{solutionName}' (exit code {process.ExitCode}).");

            log($"Solution exported to: {fullPath}");
        }

        public static async Task UnpackSolution(string solutionName, string solutionFilePath, string exportFilePath, Action<string> log)
        {
            log($"Unpacking unmanaged solution for: {solutionName}");
            var unmanagedZipFile = Path.Combine(solutionFilePath, $"{solutionName}.zip");
            var solutionExportPath = Path.Combine(exportFilePath, solutionName);

            if (File.Exists(unmanagedZipFile))
            {
                log($"Unpacking to: {solutionExportPath}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "pac",
                        Arguments = $"solution unpack --zipfile \"{unmanagedZipFile}\" --folder \"{solutionExportPath}\" --packageType Both",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                    log(output);
                if (!string.IsNullOrEmpty(error))
                    log(error);
            }
            else
            {
                log($"Unmanaged solution zip file not found: {unmanagedZipFile}");
            }
        }

        public static void CopySolutionToRepository(string solutionName, string unpackedPath, string repoSolutionsPath, Action<string> log)
        {
            var sourceContentDir = Path.Combine(unpackedPath, solutionName);
            var repoSolutionDir = Path.Combine(repoSolutionsPath, solutionName);
            var repoContentDir = Path.Combine(repoSolutionDir, solutionName);

            if (!Directory.Exists(sourceContentDir))
            {
                log($"Unpacked content folder not found: {sourceContentDir}");
                return;
            }

            if (!Directory.Exists(repoSolutionDir))
            {
                log($"Repository solution folder not found: {repoSolutionDir}");
                return;
            }

            // Remove existing solution content in repo, but preserve .csproj and obj/
            if (Directory.Exists(repoContentDir))
            {
                log($"Removing existing solution content: {repoContentDir}");
                Directory.Delete(repoContentDir, true);
            }

            log($"Copying solution content to repository: {repoContentDir}");
            CopyDirectory(sourceContentDir, repoContentDir);
            log($"Solution '{solutionName}' copied to repository.");
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
    }
}