using System;
using System.Text.Json;
using System.Threading.Tasks;
using TmdlStudio.Models;
using TmdlStudio.Services;

namespace TmdlStudio.Commands
{
    /// <summary>
    /// Command handler for deploying TMDL models to a workspace.
    /// </summary>
    public static class DeployCommand
    {
        /// <summary>
        /// Executes the deploy command.
        /// </summary>
        /// <param name="path">Path to the TMDL folder.</param>
        /// <param name="noBrowser">If true, skips browser authentication and uses device code flow.</param>
        public static async Task Execute(string path, bool noBrowser = false)
        {
            try
            {
                // Read auth config from environment variable to avoid exposing credentials in command line
                string authJson = Environment.GetEnvironmentVariable("TMDL_AUTH_CONFIG");
                
                if (string.IsNullOrEmpty(authJson))
                {
                    var errorResult = DeployResult.Error("Authentication configuration not found. Expected TMDL_AUTH_CONFIG environment variable.");
                    OutputResult(errorResult);
                    return;
                }

                var authConfig = JsonSerializer.Deserialize<AuthConfig>(authJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (authConfig == null)
                {
                    var errorResult = DeployResult.Error("Failed to parse authentication configuration");
                    OutputResult(errorResult);
                    return;
                }

                // Validate authentication configuration
                if (!ValidateAuthConfig(authConfig, out string errorMessage))
                {
                    var errorResult = DeployResult.Error(errorMessage);
                    OutputResult(errorResult);
                    return;
                }

                // Acquire access token based on authentication mode
                if (authConfig.IsServicePrincipal)
                {
                    try
                    {
                        Console.WriteLine("Acquiring access token for service principal...");
                        authConfig.AccessToken = await TokenService.AcquireTokenByServicePrincipalAsync(
                            authConfig.ClientId,
                            authConfig.ClientSecret,
                            authConfig.TenantId
                        );
                        Console.WriteLine("✓ Access token acquired");
                    }
                    catch (Exception ex)
                    {
                        var errorResult = DeployResult.Error($"Failed to acquire access token: {ex.Message}");
                        OutputResult(errorResult);
                        return;
                    }
                }
                else if (authConfig.Mode?.ToLower() == "interactive" && string.IsNullOrEmpty(authConfig.AccessToken))
                {
                    // CLI interactive mode - use browser auth with fallback to device code
                    try
                    {
                        authConfig.AccessToken = await TokenService.AcquireTokenInteractiveAsync(!noBrowser);
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("CI environment"))
                    {
                        var errorResult = DeployResult.Error(ex.Message);
                        OutputResult(errorResult);
                        return;
                    }
                    catch (Exception ex)
                    {
                        var errorResult = DeployResult.Error($"Failed to authenticate: {ex.Message}");
                        OutputResult(errorResult);
                        return;
                    }
                }

                var result = TmdlService.Deploy(path, authConfig);
                OutputResult(result);
            }
            catch (Exception ex)
            {
                var errorResult = DeployResult.Error($"Deployment command failed: {ex.Message}");
                OutputResult(errorResult);
            }
        }

        /// <summary>
        /// Validates the authentication configuration.
        /// </summary>
        /// <param name="authConfig">The authentication configuration.</param>
        /// <param name="errorMessage">Output error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool ValidateAuthConfig(AuthConfig authConfig, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(authConfig.WorkspaceUrl))
            {
                errorMessage = "Workspace URL is required";
                return false;
            }

            if (authConfig.Mode?.ToLower() == "interactive")
            {
                // Interactive mode - CLI will acquire token via device code flow
                // No credentials required upfront, user will authenticate via browser
                return true;
            }
            else if (authConfig.IsServicePrincipal)
            {
                // Service principal auth - all credentials required
                if (string.IsNullOrEmpty(authConfig.ClientId))
                {
                    errorMessage = "Client ID is required for service principal authentication";
                    return false;
                }
                if (string.IsNullOrEmpty(authConfig.ClientSecret))
                {
                    errorMessage = "Client Secret is required for service principal authentication";
                    return false;
                }
                if (string.IsNullOrEmpty(authConfig.TenantId))
                {
                    errorMessage = "Tenant ID is required for service principal authentication";
                    return false;
                }
            }
            else
            {
                errorMessage = "Invalid authentication configuration. Must provide either access token (interactive) or service principal credentials.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Outputs the deployment result as JSON.
        /// </summary>
        /// <param name="result">The deployment result.</param>
        private static void OutputResult(DeployResult result)
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            Console.WriteLine(json);
        }
    }
}
