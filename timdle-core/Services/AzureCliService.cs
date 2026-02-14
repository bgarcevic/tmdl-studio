using System;
using System.Diagnostics;

namespace TmdlStudio.Services
{
    /// <summary>
    /// Helper for reading access tokens from Azure CLI.
    /// </summary>
    public static class AzureCliService
    {
        private const string Resource = "https://analysis.windows.net/powerbi/api";

        /// <summary>
        /// Gets an access token from Azure CLI if available.
        /// </summary>
        public static string TryGetAccessToken()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "az",
                        Arguments = $"account get-access-token --resource {Resource} --query accessToken -o tsv",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(4000);

                if (process.ExitCode != 0)
                {
                    return null;
                }

                var token = output?.Trim();
                return string.IsNullOrEmpty(token) ? null : token;
            }
            catch
            {
                return null;
            }
        }
    }
}
