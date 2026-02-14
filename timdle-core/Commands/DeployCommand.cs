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
        private const string EnvWorkspaceUrl = "TMDL_WORKSPACE_URL";
        private const string EnvClientId = "TMDL_CLIENT_ID";
        private const string EnvClientSecret = "TMDL_CLIENT_SECRET";
        private const string EnvTenantId = "TMDL_TENANT_ID";

        /// <summary>
        /// Executes the deploy command.
        /// </summary>
        public static async Task Execute(
            string path,
            bool noBrowser = false,
            string workspace = null,
            string name = null,
            bool interactive = false,
            bool servicePrincipal = false,
            string clientId = null,
            string clientSecret = null,
            string tenantId = null)
        {
            try
            {
                var authConfig = ResolveAuthConfig(
                    workspace,
                    name,
                    interactive,
                    servicePrincipal,
                    clientId,
                    clientSecret,
                    tenantId);

                if (authConfig == null)
                {
                    OutputResult(DeployResult.Error("Failed to resolve authentication configuration."));
                    return;
                }

                if (!ValidateAuthConfig(authConfig, out var errorMessage))
                {
                    OutputResult(DeployResult.Error(errorMessage));
                    return;
                }

                if (authConfig.Mode?.ToLower() == "interactive")
                {
                    if (!authConfig.HasUsableAccessToken())
                    {
                        authConfig.AccessToken = await TokenService.AcquireTokenInteractiveAsync(!noBrowser);
                        authConfig.AccessTokenExpiresOn = DateTime.UtcNow.AddMinutes(55);
                    }

                    TokenCacheService.Save(authConfig);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(authConfig.ClientSecret))
                    {
                        if (TokenService.IsCiEnvironment())
                        {
                            OutputResult(DeployResult.Error($"Missing {EnvClientSecret} for service principal authentication in CI environment."));
                            return;
                        }

                        authConfig.ClientSecret = ConsolePrompter.PromptSecret("Client Secret");
                    }

                    authConfig.AccessToken = await TokenService.AcquireTokenByServicePrincipalAsync(
                        authConfig.ClientId,
                        authConfig.ClientSecret,
                        authConfig.TenantId);

                    // Do not persist service principal secret.
                    authConfig.ClientSecret = null;
                    TokenCacheService.Save(authConfig);
                }

                var result = TmdlService.Deploy(path, authConfig);
                TokenCacheService.Save(authConfig);
                OutputResult(result);
            }
            catch (InvalidOperationException ex)
            {
                OutputResult(DeployResult.Error(ex.Message));
            }
            catch (Exception ex)
            {
                OutputResult(DeployResult.Error($"Deployment command failed: {ex.Message}"));
            }
        }

        private static AuthConfig ResolveAuthConfig(
            string workspace,
            string name,
            bool interactive,
            bool servicePrincipal,
            string clientId,
            string clientSecret,
            string tenantId)
        {
            if (interactive && servicePrincipal)
            {
                throw new InvalidOperationException("Use either --interactive or --service-principal, not both.");
            }

            var config = new AuthConfig();

            // Merge order (low -> high): cache -> individual env -> legacy JSON env.
            // CLI flags are applied last as the highest priority.
            MergeMissing(config, TokenCacheService.Load());
            MergeMissing(config, ResolveFromIndividualEnvVars());
            MergeMissing(config, ResolveFromLegacyEnvVar());

            // Highest priority: explicit CLI flags/options.
            if (!string.IsNullOrWhiteSpace(workspace)) { config.WorkspaceUrl = workspace; }
            if (interactive) { config.Mode = "interactive"; }
            if (servicePrincipal) { config.Mode = "service-principal"; }
            if (!string.IsNullOrWhiteSpace(clientId)) { config.ClientId = clientId; }
            if (!string.IsNullOrWhiteSpace(clientSecret)) { config.ClientSecret = clientSecret; }
            if (!string.IsNullOrWhiteSpace(tenantId)) { config.TenantId = tenantId; }

            // Name override is highest priority, but preserve previous candidate for rename.
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (string.IsNullOrWhiteSpace(config.PreviousModelName) && !string.IsNullOrWhiteSpace(config.ModelName))
                {
                    config.PreviousModelName = config.ModelName;
                }
                config.ModelName = name;
            }

            // Infer or prompt mode if needed.
            if (string.IsNullOrWhiteSpace(config.Mode))
            {
                if (!string.IsNullOrWhiteSpace(config.ClientId) && !string.IsNullOrWhiteSpace(config.TenantId))
                {
                    config.Mode = "service-principal";
                }
                else if (!TokenService.IsCiEnvironment())
                {
                    config.Mode = ConsolePrompter.PromptChoice(
                        "Select authentication mode:",
                        "interactive",
                        "service-principal");
                }
                else
                {
                    config.Mode = "interactive";
                }
            }

            // 5) Azure CLI access token (simple fallback, interactive mode only)
            if (config.Mode == "interactive" && string.IsNullOrWhiteSpace(config.AccessToken))
            {
                var azToken = AzureCliService.TryGetAccessToken();
                if (!string.IsNullOrWhiteSpace(azToken))
                {
                    config.AccessToken = azToken;
                    config.AccessTokenExpiresOn = DateTime.UtcNow.AddMinutes(30);
                }
            }

            // 6) Interactive prompts if still incomplete and not in CI
            if (TokenService.IsCiEnvironment())
            {
                return config;
            }

            if (string.IsNullOrWhiteSpace(config.WorkspaceUrl))
            {
                config.WorkspaceUrl = ConsolePrompter.PromptRequired("Workspace URL");
            }

            if (config.Mode == "service-principal")
            {
                if (string.IsNullOrWhiteSpace(config.ClientId))
                {
                    config.ClientId = ConsolePrompter.PromptRequired("Client ID");
                }

                if (string.IsNullOrWhiteSpace(config.TenantId))
                {
                    config.TenantId = ConsolePrompter.PromptRequired("Tenant ID");
                }
            }

            return config;
        }

        private static AuthConfig ResolveFromIndividualEnvVars()
        {
            var workspace = Environment.GetEnvironmentVariable(EnvWorkspaceUrl);
            var envClientId = Environment.GetEnvironmentVariable(EnvClientId);
            var envClientSecret = Environment.GetEnvironmentVariable(EnvClientSecret);
            var envTenantId = Environment.GetEnvironmentVariable(EnvTenantId);

            if (string.IsNullOrWhiteSpace(workspace) &&
                string.IsNullOrWhiteSpace(envClientId) &&
                string.IsNullOrWhiteSpace(envClientSecret) &&
                string.IsNullOrWhiteSpace(envTenantId))
            {
                return null;
            }

            return new AuthConfig
            {
                WorkspaceUrl = workspace,
                ClientId = envClientId,
                ClientSecret = envClientSecret,
                TenantId = envTenantId,
                Mode = (!string.IsNullOrWhiteSpace(envClientId) && !string.IsNullOrWhiteSpace(envTenantId))
                    ? "service-principal"
                    : "interactive"
            };
        }

        private static AuthConfig ResolveFromLegacyEnvVar()
        {
            var authJson = Environment.GetEnvironmentVariable("TMDL_AUTH_CONFIG");
            if (string.IsNullOrWhiteSpace(authJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<AuthConfig>(authJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }
            catch
            {
                return null;
            }
        }

        private static void MergeMissing(AuthConfig target, AuthConfig source)
        {
            if (target == null || source == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(target.Mode)) { target.Mode = source.Mode; }
            if (string.IsNullOrWhiteSpace(target.WorkspaceUrl)) { target.WorkspaceUrl = source.WorkspaceUrl; }
            if (string.IsNullOrWhiteSpace(target.AccessToken)) { target.AccessToken = source.AccessToken; }
            if (!target.AccessTokenExpiresOn.HasValue) { target.AccessTokenExpiresOn = source.AccessTokenExpiresOn; }
            if (string.IsNullOrWhiteSpace(target.AccountUsername)) { target.AccountUsername = source.AccountUsername; }
            // Do not merge cached model name as an implicit override.
            // Model name should come from --name only; otherwise deploy resolves from .platform/database.
            if (string.IsNullOrWhiteSpace(target.PreviousModelName))
            {
                target.PreviousModelName = !string.IsNullOrWhiteSpace(source.PreviousModelName)
                    ? source.PreviousModelName
                    : source.ModelName;
            }
            if (string.IsNullOrWhiteSpace(target.ClientId)) { target.ClientId = source.ClientId; }
            if (string.IsNullOrWhiteSpace(target.ClientSecret)) { target.ClientSecret = source.ClientSecret; }
            if (string.IsNullOrWhiteSpace(target.TenantId)) { target.TenantId = source.TenantId; }
        }

        /// <summary>
        /// Validates the authentication configuration.
        /// </summary>
        private static bool ValidateAuthConfig(AuthConfig authConfig, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(authConfig.WorkspaceUrl))
            {
                errorMessage = "Workspace URL is required. Use --workspace or TMDL_WORKSPACE_URL.";
                return false;
            }

            if (authConfig.Mode?.ToLower() == "interactive")
            {
                return true;
            }

            if (authConfig.Mode?.ToLower() == "service-principal" || authConfig.Mode?.ToLower() == "env")
            {
                if (string.IsNullOrWhiteSpace(authConfig.ClientId))
                {
                    errorMessage = "Client ID is required for service principal authentication.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(authConfig.TenantId))
                {
                    errorMessage = "Tenant ID is required for service principal authentication.";
                    return false;
                }

                return true;
            }

            errorMessage = "Invalid authentication mode. Use interactive or service-principal.";
            return false;
        }

        /// <summary>
        /// Outputs the deployment result as JSON.
        /// </summary>
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
