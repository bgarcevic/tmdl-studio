using System;
using System.Threading.Tasks;
using TmdlStudio.Models;
using TmdlStudio.Services;

namespace TmdlStudio.Commands
{
    /// <summary>
    /// Command handler for authenticating and caching credentials.
    /// </summary>
    public static class LoginCommand
    {
        /// <summary>
        /// Executes login flow.
        /// </summary>
        public static async Task Execute(
            bool interactive,
            bool servicePrincipal,
            string clientId,
            string clientSecret,
            string tenantId,
            bool noBrowser = false)
        {
            if (interactive && servicePrincipal)
            {
                Console.WriteLine("ERROR: Use either --interactive or --service-principal, not both.");
                return;
            }

            var mode = ResolveMode(interactive, servicePrincipal);
            var config = new AuthConfig
            {
                Mode = mode
            };

            try
            {
                if (mode == "interactive")
                {
                    config.AccessToken = await TokenService.AcquireTokenInteractiveAsync(!noBrowser);
                    config.AccessTokenExpiresOn = DateTime.UtcNow.AddMinutes(55);
                }
                else
                {
                    if (IsNonInteractive() &&
                        (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(clientSecret)))
                    {
                        throw new InvalidOperationException(
                            "Non-interactive environment detected. For service-principal login, provide --client-id, --tenant-id, and --client-secret.");
                    }

                    config.ClientId = string.IsNullOrWhiteSpace(clientId)
                        ? ConsolePrompter.PromptRequired("Client ID")
                        : clientId;

                    config.TenantId = string.IsNullOrWhiteSpace(tenantId)
                        ? ConsolePrompter.PromptRequired("Tenant ID")
                        : tenantId;

                    var resolvedSecret = string.IsNullOrWhiteSpace(clientSecret)
                        ? ConsolePrompter.PromptSecret("Client Secret")
                        : clientSecret;

                    // Validate credentials now, but do not persist secret.
                    await TokenService.AcquireTokenByServicePrincipalAsync(
                        config.ClientId,
                        resolvedSecret,
                        config.TenantId);

                    Console.WriteLine("✓ Service principal authentication validated.");
                }

                TokenCacheService.Save(config);
                Console.WriteLine($"✓ Login successful. Auth cached at {TokenCacheService.GetCacheFilePath()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Login failed: {ex.Message}");
            }
        }

        private static string ResolveMode(bool interactive, bool servicePrincipal)
        {
            if (interactive)
            {
                return "interactive";
            }

            if (servicePrincipal)
            {
                return "service-principal";
            }

            if (IsNonInteractive())
            {
                throw new InvalidOperationException(
                    "Non-interactive environment detected. Use --interactive or --service-principal explicitly.");
            }

            var choice = ConsolePrompter.PromptChoice(
                "Select authentication mode:",
                "interactive",
                "service-principal");

            return choice;
        }

        private static bool IsNonInteractive()
        {
            return TokenService.IsCiEnvironment() || Console.IsInputRedirected;
        }
    }
}
