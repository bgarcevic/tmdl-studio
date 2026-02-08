/**
 * Authentication types for TMDL Studio deployment.
 */

/**
 * Authentication mode for deployment.
 */
export type AuthMode = 'interactive' | 'service-principal' | 'env';

/**
 * Result of an authentication operation.
 */
export interface AuthResult {
    accessToken: string;
    expiresOn: Date;
    account?: AccountInfo;
}

/**
 * Account information from authentication.
 */
export interface AccountInfo {
    username: string;
    name?: string;
}

/**
 * Service principal credentials.
 */
export interface ServicePrincipalCredentials {
    clientId: string;
    clientSecret: string;
    tenantId: string;
}

/**
 * Token cache entry for storing authentication tokens.
 */
export interface TokenCacheEntry {
    accessToken: string;
    refreshToken?: string;
    expiresOn: string;
    account: AccountInfo;
}

/**
 * Authentication configuration for CLI deployment.
 */
export interface AuthConfig {
    mode: AuthMode;
    workspaceUrl: string;
    // For token-based auth
    accessToken?: string;
    // For service principal auth
    clientId?: string;
    clientSecret?: string;
    tenantId?: string;
}

/**
 * Environment variable names for CI/CD authentication.
 */
export const AUTH_ENV_VARS = {
    workspaceUrl: 'TMDL_WORKSPACE_URL',
    clientId: 'TMDL_CLIENT_ID',
    clientSecret: 'TMDL_CLIENT_SECRET',
    tenantId: 'TMDL_TENANT_ID'
} as const;

/**
 * MSAL configuration constants.
 * 
 * NOTE: This uses the Azure CLI client ID for device code flow authentication.
 * This is a well-known public client ID that Microsoft allows for CLI tools.
 * For production deployment, consider registering your own app in Azure AD and
 * updating this client ID with your registered application's ID.
 * 
 * See: https://learn.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app
 */
export const MSAL_CONFIG = {
    authority: 'https://login.microsoftonline.com/common',
    clientId: '04b07795-8ddb-461a-bbee-02f9e1bf7b46', // Azure CLI well-known client ID
    scopes: ['https://analysis.windows.net/powerbi/api/.default']
} as const;
