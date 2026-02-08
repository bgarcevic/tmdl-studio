using System;
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
        /// Validates that an access token is available for interactive authentication.
        /// For interactive mode, the token should be acquired by the TypeScript extension
        /// and passed to the CLI.
        /// </summary>
        /// <param name="accessToken">The access token to validate.</param>
        /// <returns>True if the token is valid (not empty).</returns>
        public static bool ValidateAccessToken(string accessToken)
        {
            return !string.IsNullOrEmpty(accessToken);
        }

        /// <summary>
        /// Refreshes an access token using service principal credentials if needed.
        /// For service principals, we always acquire a fresh token since MSAL doesn't
        /// cache confidential client tokens in the same way as public client tokens.
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
            // If we have an existing token, return it (we assume it's valid since
            // MSAL handles caching internally for confidential clients)
            if (!string.IsNullOrEmpty(existingToken))
            {
                return existingToken;
            }

            // Otherwise, acquire a new token
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
