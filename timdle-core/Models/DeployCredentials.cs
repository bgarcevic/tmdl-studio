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
        /// Checks if this is an interactive (token-based) authentication.
        /// </summary>
        public bool IsInteractive => Mode?.ToLower() == "interactive" && !string.IsNullOrEmpty(AccessToken);

        /// <summary>
        /// Checks if this is service principal authentication.
        /// </summary>
        public bool IsServicePrincipal =>
            (Mode?.ToLower() == "service-principal" || Mode?.ToLower() == "env") &&
            !string.IsNullOrEmpty(ClientId) &&
            !string.IsNullOrEmpty(ClientSecret) &&
            !string.IsNullOrEmpty(TenantId);
    }
}
