import * as vscode from 'vscode';
import * as https from 'https';
import { PublicClientApplication, DeviceCodeRequest, SilentFlowRequest, AuthenticationResult } from '@azure/msal-node';
import {
    AuthMode,
    AuthResult,
    ServicePrincipalCredentials,
    TokenCacheEntry,
    AUTH_ENV_VARS,
    MSAL_CONFIG
} from '../types/auth';

/**
 * Service for handling authentication with Azure/Power BI.
 * Supports interactive (browser), service principal, and environment variable modes.
 */
export class AuthService {
    private msalClient: PublicClientApplication;
    private outputChannel: vscode.OutputChannel;

    private static readonly TOKEN_CACHE_KEY = 'tmdl-auth-token-cache';
    private static readonly ACCOUNT_CACHE_KEY = 'tmdl-auth-account';

    /**
     * Creates a new AuthService instance.
     * @param context - The VS Code extension context.
     */
    constructor(private context: vscode.ExtensionContext) {
        this.msalClient = new PublicClientApplication({
            auth: {
                clientId: MSAL_CONFIG.clientId,
                authority: MSAL_CONFIG.authority
            }
        });
        this.outputChannel = vscode.window.createOutputChannel('TMDL Auth');
    }

    /**
     * Authenticates using the specified mode.
     * @param mode - The authentication mode.
     * @returns The authentication result with access token.
     */
    async authenticate(mode: AuthMode): Promise<AuthResult> {
        switch (mode) {
            case 'interactive':
                return this.authenticateInteractive();
            case 'service-principal':
                return this.authenticateServicePrincipal();
            case 'env':
                return this.authenticateFromEnv();
            default:
                throw new Error(`Unknown authentication mode: ${mode}`);
        }
    }

    /**
     * Authenticates interactively using device code flow.
     * Opens browser for user to sign in.
     * @returns The authentication result.
     */
    private async authenticateInteractive(): Promise<AuthResult> {
        // Try silent authentication first
        const cachedToken = await this.getCachedToken();
        if (cachedToken) {
            this.outputChannel.appendLine('Using cached token');
            return cachedToken;
        }

        // Perform device code flow
        const deviceCodeRequest: DeviceCodeRequest = {
            scopes: [...MSAL_CONFIG.scopes],
            deviceCodeCallback: (response) => {
                // Show the code to the user
                vscode.window.showInformationMessage(
                    `To sign in, use code: ${response.userCode}`,
                    'Copy Code'
                ).then(selection => {
                    if (selection === 'Copy Code') {
                        vscode.env.clipboard.writeText(response.userCode);
                    }
                });

                // Open the browser
                vscode.env.openExternal(vscode.Uri.parse(response.verificationUri));

                this.outputChannel.show();
                this.outputChannel.appendLine(`\nTo sign in to Power BI:`);
                this.outputChannel.appendLine(`1. Open: ${response.verificationUri}`);
                this.outputChannel.appendLine(`2. Enter code: ${response.userCode}`);
                this.outputChannel.appendLine(`3. Complete sign in\n`);
            }
        };

        try {
            this.outputChannel.appendLine('Initiating device code flow...');
            const result = await this.msalClient.acquireTokenByDeviceCode(deviceCodeRequest);

            if (!result || !result.accessToken) {
                throw new Error('Authentication failed - no token received');
            }

            // Cache the token
            await this.cacheToken(result);

            return {
                accessToken: result.accessToken,
                expiresOn: result.expiresOn || new Date(Date.now() + 3600 * 1000),
                account: result.account ? {
                    username: result.account.username,
                    name: result.account.name || undefined
                } : undefined
            };
        } catch (error) {
            this.outputChannel.appendLine(`Authentication error: ${error}`);
            throw new Error(`Failed to authenticate: ${error}`);
        }
    }

    /**
     * Refreshes the token if needed.
     * @returns The valid access token, or null if refresh failed.
     */
    async refreshTokenIfNeeded(): Promise<string | null> {
        const cachedAccount = await this.context.secrets.get(AuthService.ACCOUNT_CACHE_KEY);
        if (!cachedAccount) {
            return null;
        }

        const account = JSON.parse(cachedAccount);

        const silentRequest: SilentFlowRequest = {
            account: account,
            scopes: [...MSAL_CONFIG.scopes]
        };

        try {
            const result = await this.msalClient.acquireTokenSilent(silentRequest);
            if (result && result.accessToken) {
                await this.cacheToken(result);
                return result.accessToken;
            }
        } catch (error) {
            this.outputChannel.appendLine(`Token refresh failed: ${error}`);
        }

        return null;
    }

    /**
     * Authenticates using service principal credentials from user input.
     * @returns The authentication result.
     */
    private async authenticateServicePrincipal(): Promise<AuthResult> {
        const credentials = await this.promptForServicePrincipalCredentials();

        // For service principal, we don't do interactive auth
        // Instead, we pass credentials to CLI for server-side auth
        return {
            accessToken: '', // Will be obtained by CLI
            expiresOn: new Date(Date.now() + 3600 * 1000),
            account: {
                username: `Service Principal: ${credentials.clientId}`
            }
        };
    }

    /**
     * Authenticates using environment variables.
     * @returns The authentication result.
     */
    private async authenticateFromEnv(): Promise<AuthResult> {
        const credentials = this.loadCredentialsFromEnv();

        if (!credentials) {
            throw new Error(
                `Environment variables not found. Required: ${AUTH_ENV_VARS.workspaceUrl}, ` +
                `${AUTH_ENV_VARS.clientId}, ${AUTH_ENV_VARS.clientSecret}, ${AUTH_ENV_VARS.tenantId}`
            );
        }

        return {
            accessToken: '', // Will be obtained by CLI
            expiresOn: new Date(Date.now() + 3600 * 1000),
            account: {
                username: `Environment: ${credentials.clientId}`
            }
        };
    }

    /**
     * Prompts user for service principal credentials.
     * @returns The service principal credentials.
     */
    private async promptForServicePrincipalCredentials(): Promise<ServicePrincipalCredentials> {
        const clientId = await vscode.window.showInputBox({
            prompt: 'Enter the Service Principal Client ID',
            placeHolder: 'Client ID',
            ignoreFocusOut: true
        });
        if (!clientId) { throw new Error('Client ID is required'); }

        const clientSecret = await vscode.window.showInputBox({
            prompt: 'Enter the Service Principal Client Secret',
            placeHolder: 'Client Secret',
            password: true,
            ignoreFocusOut: true
        });
        if (!clientSecret) { throw new Error('Client Secret is required'); }

        const tenantId = await vscode.window.showInputBox({
            prompt: 'Enter the Tenant ID',
            placeHolder: 'Tenant ID',
            ignoreFocusOut: true
        });
        if (!tenantId) { throw new Error('Tenant ID is required'); }

        return { clientId, clientSecret, tenantId };
    }

    /**
     * Loads service principal credentials from environment variables.
     * @returns The credentials if all env vars are set, null otherwise.
     */
    loadCredentialsFromEnv(): ServicePrincipalCredentials | null {
        const clientId = process.env[AUTH_ENV_VARS.clientId];
        const clientSecret = process.env[AUTH_ENV_VARS.clientSecret];
        const tenantId = process.env[AUTH_ENV_VARS.tenantId];

        if (clientId && clientSecret && tenantId) {
            return { clientId, clientSecret, tenantId };
        }

        return null;
    }

    /**
     * Gets the workspace URL from environment variable.
     * @returns The workspace URL if set, null otherwise.
     */
    getWorkspaceUrlFromEnv(): string | null {
        return process.env[AUTH_ENV_VARS.workspaceUrl] || null;
    }

    /**
     * Checks if environment variables are configured.
     * @returns True if all required env vars are present.
     */
    hasEnvCredentials(): boolean {
        return !!(
            process.env[AUTH_ENV_VARS.workspaceUrl] &&
            process.env[AUTH_ENV_VARS.clientId] &&
            process.env[AUTH_ENV_VARS.clientSecret] &&
            process.env[AUTH_ENV_VARS.tenantId]
        );
    }

    /**
     * Caches an authentication token.
     * @param result - The MSAL authentication result.
     */
    private async cacheToken(result: AuthenticationResult): Promise<void> {
        if (!result.account) { return; }

        const cacheEntry: TokenCacheEntry = {
            accessToken: result.accessToken,
            expiresOn: result.expiresOn ? result.expiresOn.toISOString() : new Date(Date.now() + 3600 * 1000).toISOString(),
            account: {
                username: result.account.username,
                name: result.account.name || undefined
            }
        };

        await this.context.secrets.store(AuthService.TOKEN_CACHE_KEY, JSON.stringify(cacheEntry));
        await this.context.secrets.store(AuthService.ACCOUNT_CACHE_KEY, JSON.stringify(result.account));
    }

    /**
     * Gets a cached token if it's still valid.
     * @returns The cached auth result, or null if no valid token exists.
     */
    private async getCachedToken(): Promise<AuthResult | null> {
        const cached = await this.context.secrets.get(AuthService.TOKEN_CACHE_KEY);
        if (!cached) { return null; }

        try {
            const entry: TokenCacheEntry = JSON.parse(cached);
            const expiresOn = new Date(entry.expiresOn);

            // Check if token is still valid (with 5 minute buffer)
            if (expiresOn.getTime() - Date.now() > 5 * 60 * 1000) {
                return {
                    accessToken: entry.accessToken,
                    expiresOn: expiresOn,
                    account: entry.account
                };
            }

            this.outputChannel.appendLine('Cached token expired or expiring soon');
        } catch (error) {
            this.outputChannel.appendLine(`Failed to parse cached token: ${error}`);
        }

        return null;
    }

    /**
     * Clears cached authentication tokens.
     */
    async clearCache(): Promise<void> {
        await this.context.secrets.delete(AuthService.TOKEN_CACHE_KEY);
        await this.context.secrets.delete(AuthService.ACCOUNT_CACHE_KEY);
        this.outputChannel.appendLine('Authentication cache cleared');
    }

    /**
     * Fetches the list of workspaces the user has access to from Fabric API.
     * @param accessToken - The access token for authentication.
     * @returns Array of workspace information.
     */
    async getWorkspaces(accessToken: string): Promise<WorkspaceInfo[]> {
        return new Promise((resolve, reject) => {
            const options = {
                hostname: 'api.fabric.microsoft.com',
                path: '/v1/workspaces',
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${accessToken}`,
                    'Content-Type': 'application/json'
                }
            };

            const req = https.request(options, (res) => {
                let data = '';

                res.on('data', (chunk) => {
                    data += chunk;
                });

                res.on('end', () => {
                    try {
                        if (res.statusCode === 200) {
                            const response = JSON.parse(data);
                            const workspaces: WorkspaceInfo[] = response.value.map((ws: any) => ({
                                id: ws.id,
                                name: ws.displayName,
                                description: ws.description || '',
                                type: ws.type || 'Workspace'
                            }));
                            resolve(workspaces);
                        } else {
                            reject(new Error(`Failed to fetch workspaces: ${res.statusCode} - ${data}`));
                        }
                    } catch (error) {
                        reject(new Error(`Failed to parse workspaces response: ${error}`));
                    }
                });
            });

            req.on('error', (error) => {
                reject(new Error(`Request failed: ${error.message}`));
            });

            req.end();
        });
    }
}

/**
 * Information about a Fabric workspace.
 */
export interface WorkspaceInfo {
    id: string;
    name: string;
    description: string;
    type: string;
}
