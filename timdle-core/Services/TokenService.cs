using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace TmdlStudio.Services
{
    /// <summary>
    /// Service for acquiring access tokens for Power BI authentication.
    /// Supports both service principal and interactive (delegated) authentication.
    /// </summary>
    public static class TokenService
    {
        private const string PowerBiResource = "https://analysis.windows.net/powerbi/api/.default";
        private const string AuthorityBase = "https://login.microsoftonline.com";

        /// <summary>
        /// Azure CLI well-known client ID for device code flow.
        /// This public client ID is allowed for CLI tools to authenticate users.
        /// </summary>
        public const string AzureCliClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";

        /// <summary>
        /// Acquires an access token using service principal credentials.
        /// </summary>
        /// <param name="clientId">The service principal client ID.</param>
        /// <param name="clientSecret">The service principal client secret.</param>
        /// <param name="tenantId">The Azure AD tenant ID.</param>
        /// <returns>The access token.</returns>
        public static async Task<string> AcquireTokenByServicePrincipalAsync(
            string clientId,
            string clientSecret,
            string tenantId)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentException("Client ID is required", nameof(clientId));
            if (string.IsNullOrEmpty(clientSecret))
                throw new ArgumentException("Client Secret is required", nameof(clientSecret));
            if (string.IsNullOrEmpty(tenantId))
                throw new ArgumentException("Tenant ID is required", nameof(tenantId));

            var authority = $"{AuthorityBase}/{tenantId}";

            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(authority)
                .Build();

            var scopes = new[] { PowerBiResource };

            try
            {
                var result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();

                return result.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                throw new Exception($"Failed to acquire token for service principal: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Acquires an access token using interactive authentication.
        /// Automatically tries browser-based auth first, falls back to device code if needed.
        /// In CI/CD environments, skips browser and goes directly to device code or fails if service principal is required.
        /// </summary>
        /// <param name="useBrowser">Whether to attempt browser-based authentication.</param>
        /// <returns>The access token.</returns>
        public static async Task<string> AcquireTokenInteractiveAsync(bool useBrowser = true)
        {
            var app = PublicClientApplicationBuilder
                .Create(AzureCliClientId)
                .WithAuthority($"{AuthorityBase}/common")
                .WithRedirectUri("http://localhost") // Required for interactive browser flow
                .Build();

            var scopes = new[] { PowerBiResource };

            try
            {
                // Try silent authentication first (cached token)
                var accounts = await app.GetAccountsAsync();
                if (accounts.Any())
                {
                    try
                    {
                        var silentResult = await app.AcquireTokenSilent(scopes, accounts.First())
                            .ExecuteAsync();
                        Console.WriteLine("✓ Using cached credentials");
                        return silentResult.AccessToken;
                    }
                    catch (MsalUiRequiredException)
                    {
                        // Silent auth failed, continue to interactive
                    }
                }

                // Check if we're in a CI environment
                if (IsCiEnvironment())
                {
                    throw new InvalidOperationException(
                        "CI environment detected. Browser authentication is not available in CI/CD pipelines. " +
                        "Please use service principal authentication instead:\n" +
                        "export TMDL_AUTH_CONFIG='{\"mode\":\"service-principal\",\"clientId\":\"...\",\"clientSecret\":\"...\",\"tenantId\":\"...\",\"workspaceUrl\":\"...\"}'"
                    );
                }

                // Try browser-based authentication if enabled
                if (useBrowser)
                {
                    try
                    {
                        Console.WriteLine("Opening browser for authentication...");
                        var browserResult = await app.AcquireTokenInteractive(scopes)
                            .WithUseEmbeddedWebView(false) // Use system browser
                            .ExecuteAsync();
                        Console.WriteLine("✓ Authentication successful");
                        return browserResult.AccessToken;
                    }
                    catch (MsalException ex) when (ex.ErrorCode == "authentication_canceled" || 
                                                   ex.Message.Contains("browser") ||
                                                   ex.Message.Contains("platform"))
                    {
                        Console.WriteLine("Browser authentication not available. Falling back to device code...");
                        // Fall through to device code
                    }
                }

                // Fall back to device code flow
                return await AcquireTokenByDeviceCodeAsync(app, scopes);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new Exception($"Failed to acquire token: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Acquires an access token using device code flow for interactive authentication.
        /// </summary>
        /// <param name="app">The public client application.</param>
        /// <param name="scopes">The scopes to request.</param>
        /// <param name="useClipboard">Whether to try copying the code to clipboard.</param>
        /// <returns>The access token.</returns>
        private static async Task<string> AcquireTokenByDeviceCodeAsync(IPublicClientApplication app, string[] scopes, bool useClipboard = true)
        {
            try
            {
                string userCode = null;
                string verificationUrl = null;

                var result = await app.AcquireTokenWithDeviceCode(
                    scopes,
                    deviceCodeResult =>
                    {
                        userCode = deviceCodeResult.UserCode;
                        verificationUrl = deviceCodeResult.VerificationUrl;

                        // Try to copy code to clipboard (best effort)
                        if (useClipboard)
                        {
                            TryCopyToClipboard(userCode);
                        }

                        // Format the box dynamically to handle varying URL lengths
                        const int boxWidth = 58;
                        var urlLine = $"  1. Visit: {verificationUrl}";
                        var codeLine = $"  2. Enter code: {userCode}";
                        
                        // Pad or truncate to fit box
                        urlLine = urlLine.Length > boxWidth - 2 ? urlLine.Substring(0, boxWidth - 5) + "..." : urlLine.PadRight(boxWidth - 2);
                        codeLine = codeLine.PadRight(boxWidth - 2);

                        Console.WriteLine();
                        Console.WriteLine("╔" + new string('═', boxWidth) + "╗");
                        Console.WriteLine("║" + " To sign in to Microsoft Fabric:".PadRight(boxWidth) + "║");
                        Console.WriteLine("║" + new string(' ', boxWidth) + "║");
                        Console.WriteLine("║" + urlLine + "║");
                        Console.WriteLine("║" + codeLine + "║");
                        Console.WriteLine("║" + new string(' ', boxWidth) + "║");
                        Console.WriteLine("║" + " Code copied to clipboard (if supported)".PadRight(boxWidth) + "║");
                        Console.WriteLine("╚" + new string('═', boxWidth) + "╝");
                        Console.WriteLine();
                        Console.Write("Waiting for authentication");

                        return Task.CompletedTask;
                    }).ExecuteAsync();

                Console.WriteLine(" ✓");
                Console.WriteLine("Authentication successful!");
                return result.AccessToken;
            }
            catch (MsalServiceException ex)
            {
                Console.WriteLine(); // New line after the dots
                throw new Exception($"Failed to acquire token via device code: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Acquires an access token using device code flow (legacy method without app instance).
        /// </summary>
        /// <param name="useClipboard">Whether to try copying the code to clipboard.</param>
        /// <returns>The access token.</returns>
        public static async Task<string> AcquireTokenByDeviceCodeAsync(bool useClipboard = true)
        {
            var app = PublicClientApplicationBuilder
                .Create(AzureCliClientId)
                .WithAuthority($"{AuthorityBase}/common")
                .Build();

            var scopes = new[] { PowerBiResource };
            return await AcquireTokenByDeviceCodeAsync(app, scopes, useClipboard);
        }

        /// <summary>
        /// Detects if running in a CI/CD environment.
        /// </summary>
        /// <returns>True if CI environment variables are detected.</returns>
        public static bool IsCiEnvironment()
        {
            var ciVars = new[] { "CI", "GITHUB_ACTIONS", "TF_BUILD", "GITLAB_CI", 
                                "JENKINS_URL", "CIRCLECI", "BUILDKITE", "DRONE", 
                                "TRAVIS", "APPVEYOR", "CODEBUILD_BUILD_ID" };
            
            return ciVars.Any(var => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(var)));
        }

        /// <summary>
        /// Attempts to copy text to the system clipboard.
        /// Works on macOS, Windows, and Linux (if xclip or wl-copy is available).
        /// </summary>
        /// <param name="text">The text to copy.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool TryCopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS: use pbcopy
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "pbcopy",
                            RedirectStandardInput = true,
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                    process.StandardInput.Write(text);
                    process.StandardInput.Close();
                    return process.WaitForExit(1000) && process.ExitCode == 0;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows: use clip
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "clip",
                            RedirectStandardInput = true,
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                    process.StandardInput.Write(text);
                    process.StandardInput.Close();
                    return process.WaitForExit(1000) && process.ExitCode == 0;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux: try wl-copy (Wayland) first, then xclip (X11)
                    if (TryExecuteCommand("wl-copy", text))
                        return true;
                    
                    if (TryExecuteCommand("xclip", text, "-selection", "clipboard"))
                        return true;
                }
            }
            catch
            {
                // Ignore clipboard failures - it's a nice-to-have feature
            }

            return false;
        }

        /// <summary>
        /// Attempts to execute a command with input.
        /// </summary>
        private static bool TryExecuteCommand(string command, string input, params string[] args)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = string.Join(" ", args),
                        RedirectStandardInput = true,
                        UseShellExecute = false
                    }
                };
                process.Start();
                process.StandardInput.Write(input);
                process.StandardInput.Close();
                return process.WaitForExit(1000) && process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that an access token is available for interactive authentication.
        /// </summary>
        /// <param name="accessToken">The access token to validate.</param>
        /// <returns>True if the token is valid (not empty).</returns>
        public static bool ValidateAccessToken(string accessToken)
        {
            return !string.IsNullOrEmpty(accessToken);
        }

        /// <summary>
        /// Refreshes an access token using service principal credentials if needed.
        /// </summary>
        /// <param name="clientId">The service principal client ID.</param>
        /// <param name="clientSecret">The service principal client secret.</param>
        /// <param name="tenantId">The Azure AD tenant ID.</param>
        /// <param name="existingToken">The existing token (can be null or empty).</param>
        /// <returns>The refreshed access token, or null if refresh failed.</returns>
        public static async Task<string> RefreshTokenIfNeededAsync(
            string clientId,
            string clientSecret,
            string tenantId,
            string existingToken)
        {
            if (!string.IsNullOrEmpty(existingToken))
            {
                return existingToken;
            }

            try
            {
                return await AcquireTokenByServicePrincipalAsync(clientId, clientSecret, tenantId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the Power BI resource URL for token scope.
        /// </summary>
        public static string PowerBiResourceUrl => PowerBiResource;
    }
}
