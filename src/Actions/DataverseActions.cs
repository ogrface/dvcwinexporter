using System.Diagnostics;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CrmSolutionExporter
{
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

        public static ServiceClient ConnectToDataverse(string serverUrl)
        {
            if (!serverUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                serverUrl = "https://" + serverUrl;
            }

            var connectionString = $"AuthType=OAuth;Url={serverUrl};AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri=http://localhost;LoginPrompt=Auto";

            var serviceClient = new ServiceClient(connectionString);
            return serviceClient;
        }

        public static List<Entity> GetUnmanagedSolutions(ServiceClient serviceClient)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("uniquename", "friendlyname", "version"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("ismanaged", ConditionOperator.Equal, false)
                    }
                },
                Orders =
                {
                    new OrderExpression("uniquename", OrderType.Ascending)
                }
            };

            var results = serviceClient.RetrieveMultiple(query);
            return results.Entities.ToList();
        }

        public static void ExportSolution(ServiceClient serviceClient, string solutionName,
            string solutionFilePath, bool managed, Action<string> log)
        {
            var suffix = managed ? "_managed" : "";
            var fileName = $"{solutionName}{suffix}.zip";
            var fullPath = Path.Combine(solutionFilePath, fileName);

            var exportRequest = new Microsoft.Crm.Sdk.Messages.ExportSolutionRequest
            {
                SolutionName = solutionName,
                Managed = managed
            };

            var response = (Microsoft.Crm.Sdk.Messages.ExportSolutionResponse)serviceClient.Execute(exportRequest);
            File.WriteAllBytes(fullPath, response.ExportSolutionFile);

            log($"Solution exported to: {fullPath}");
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
                        RedirectStandardError = true
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
    }
}