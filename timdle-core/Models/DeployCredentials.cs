using System;

namespace TmdlStudio.Models
{
    /// <summary>
    /// Authentication configuration for deployment.
    /// </summary>
    public class AuthConfig
    {
        /// <summary>
        /// Authentication mode: interactive, service-principal, or env.
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// The workspace URL to deploy to.
        /// </summary>
        public string WorkspaceUrl { get; set; }

        /// <summary>
        /// Access token for interactive authentication.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Access token expiration in UTC (if known).
        /// </summary>
        public DateTime? AccessTokenExpiresOn { get; set; }

        /// <summary>
        /// Account username for cached interactive authentication.
        /// </summary>
        public string AccountUsername { get; set; }

        /// <summary>
        /// Semantic model display name override/cached value.
        /// Used by deploy when resolving the target item name.
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Previous model name candidate used when --name override is supplied.
        /// Helps locate existing item for rename before updating definition.
        /// </summary>
        public string PreviousModelName { get; set; }

        /// <summary>
        /// Client ID for service principal authentication.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Client secret for service principal authentication.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Tenant ID for service principal authentication.
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// Checks if this is an interactive authentication.
        /// For CLI usage, token will be acquired via device code flow.
        /// For VS Code extension usage, token is pre-acquired and passed in.
        /// </summary>
        public bool IsInteractive => Mode?.ToLower() == "interactive";

        /// <summary>
        /// Checks if this is service principal authentication.
        /// </summary>
        public bool IsServicePrincipal =>
            (Mode?.ToLower() == "service-principal" || Mode?.ToLower() == "env") &&
            !string.IsNullOrEmpty(ClientId) &&
            !string.IsNullOrEmpty(ClientSecret) &&
            !string.IsNullOrEmpty(TenantId);

        /// <summary>
        /// Checks if a cached interactive token is still likely usable.
        /// </summary>
        public bool HasUsableAccessToken()
        {
            if (string.IsNullOrEmpty(AccessToken))
            {
                return false;
            }

            if (!AccessTokenExpiresOn.HasValue)
            {
                return true;
            }

            // 5-minute refresh buffer.
            return AccessTokenExpiresOn.Value > DateTime.UtcNow.AddMinutes(5);
        }
    }
}
