import * as vscode from 'vscode';
import { TimdleClient, DeployResult } from '../cli/TimdleClient';
import { ProjectRootDetector } from '../utils/ProjectRootDetector';
import { TabularTreeProvider } from '../views/explorer/TabularTreeProvider';
import { AuthService, WorkspaceInfo } from '../services/AuthService';
import { DeployConfig } from '../config/DeployConfig';
import { AuthMode, AuthConfig, AUTH_ENV_VARS } from '../types/auth';

/**
 * Command handler for deploying TMDL models to a workspace.
 * Supports interactive (browser), service principal, and environment variable authentication.
 */
export class DeployCommand {
    private cliClient: TimdleClient;
    private authService: AuthService;
    private deployConfig: DeployConfig;
    private outputChannel: vscode.OutputChannel;

    /**
     * Creates a new DeployCommand instance.
     * @param context - The VS Code extension context.
     * @param treeProvider - The tree provider to get the current model from.
     */
    constructor(
        private context: vscode.ExtensionContext,
        private treeProvider?: TabularTreeProvider
    ) {
        this.cliClient = new TimdleClient(context);
        this.authService = new AuthService(context);
        this.deployConfig = new DeployConfig(context);
        this.outputChannel = vscode.window.createOutputChannel('TMDL Deploy');
    }

    /**
     * Registers the deploy command with VS Code.
     * @param context - The VS Code extension context.
     * @param treeProvider - The tree provider to get the current model from.
     * @returns The disposable command registration.
     */
    static register(context: vscode.ExtensionContext, treeProvider?: TabularTreeProvider): vscode.Disposable {
        const command = new DeployCommand(context, treeProvider);
        return vscode.commands.registerCommand('tmdl-studio.deploy', () => command.execute());
    }

    /**
     * Executes the deploy command.
     */
    private async execute(): Promise<void> {
        const projectRoot = await this.getProjectRoot();
        if (!projectRoot) {
            return;
        }

        // Select authentication mode
        const authMode = await this.selectAuthMode();
        if (!authMode) {
            return;
        }

        // Authenticate
        let authResult;
        try {
            this.outputChannel.show();
            this.outputChannel.appendLine(`Authenticating using ${authMode} mode...`);
            authResult = await this.authService.authenticate(authMode);
            this.outputChannel.appendLine(`Authenticated as: ${authResult.account?.username || 'Unknown'}`);
        } catch (error) {
            vscode.window.showErrorMessage(`Authentication failed: ${error instanceof Error ? error.message : String(error)}`);
            return;
        }

        // Build auth config for CLI
        const authConfig: AuthConfig = {
            mode: authMode,
            workspaceUrl: '' // Will be set below
        };

        // Add token for interactive mode
        if (authMode === 'interactive') {
            authConfig.accessToken = authResult.accessToken;
        }

        // Add service principal credentials for non-interactive modes
        if (authMode === 'service-principal' || authMode === 'env') {
            const credentials = authMode === 'env'
                ? this.authService.loadCredentialsFromEnv()
                : await this.promptForServicePrincipalCredentials();

            if (!credentials) {
                if (authMode === 'env') {
                    vscode.window.showErrorMessage(
                        `Environment variables not found. Required: ${AUTH_ENV_VARS.workspaceUrl}, ` +
                        `${AUTH_ENV_VARS.clientId}, ${AUTH_ENV_VARS.clientSecret}, ${AUTH_ENV_VARS.tenantId}`
                    );
                } else {
                    vscode.window.showErrorMessage('Service Principal credentials are required for deployment.');
                }
                return;
            }

            authConfig.clientId = credentials.clientId;
            authConfig.clientSecret = credentials.clientSecret;
            authConfig.tenantId = credentials.tenantId;
        }

        // Get workspace URL (with access token for interactive mode to enable workspace picker)
        const accessTokenForWorkspaceList = authMode === 'interactive' ? authResult.accessToken : undefined;
        const workspaceUrl = await this.getWorkspaceUrl(projectRoot, authMode, accessTokenForWorkspaceList);
        if (!workspaceUrl) {
            return;
        }

        authConfig.workspaceUrl = workspaceUrl;

        // Show deployment preview
        const shouldDeploy = await this.showDeployPreview(projectRoot, workspaceUrl, authMode);
        if (!shouldDeploy) {
            return;
        }

        // Execute deployment
        this.outputChannel.appendLine(`\nDeploying to workspace: ${workspaceUrl}...`);
        this.outputChannel.appendLine(`Project root: ${projectRoot}`);
        this.outputChannel.appendLine(`Authentication: ${authMode}\n`);

        try {
            const result = await this.cliClient.deploy(projectRoot, authConfig);
            this.handleDeployResult(result);
        } catch (error) {
            this.outputChannel.appendLine(`Error during deployment: ${error}`);
            vscode.window.showErrorMessage(`Deployment failed: ${error}`);
        }
    }

    /**
     * Gets the project root directory.
     * @returns The project root path, or null if not found.
     */
    private async getProjectRoot(): Promise<string | null> {
        // First, try to use the currently loaded model from the tree provider
        if (this.treeProvider) {
            const currentFolder = this.treeProvider.getCurrentFolder();
            if (currentFolder) {
                return currentFolder;
            }
        }

        // If no model is loaded in the tree, try to detect from workspace
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            vscode.window.showErrorMessage('Please open a folder containing TMDL files.');
            return null;
        }

        const rootPath = workspaceFolders[0].uri.fsPath;
        const projectRoot = ProjectRootDetector.detectProjectRoot(rootPath);

        if (!projectRoot) {
            vscode.window.showErrorMessage('Could not detect TMDL project root. Make sure the folder contains definition.pbism, .platform, definition folder, or .tmdl files.');
            return null;
        }

        return projectRoot;
    }

    /**
     * Shows a quick pick for selecting authentication mode.
     * @returns The selected auth mode, or undefined if cancelled.
     */
    private async selectAuthMode(): Promise<AuthMode | undefined> {
        interface AuthModeItem extends vscode.QuickPickItem {
            mode: AuthMode;
        }

        const items: AuthModeItem[] = [
            {
                label: '$(sign-in) Sign in with Microsoft',
                description: 'Interactive browser authentication',
                detail: 'Sign in using your Microsoft account via browser',
                mode: 'interactive'
            },
            {
                label: '$(key) Service Principal',
                description: 'Client ID and Secret authentication',
                detail: 'Use service principal credentials for non-interactive scenarios',
                mode: 'service-principal'
            }
        ];

        // Add environment variable option if env vars are configured
        if (this.authService.hasEnvCredentials()) {
            items.push({
                label: '$(globe) Environment Variables',
                description: 'Use CI/CD environment variables',
                detail: `Using: ${AUTH_ENV_VARS.clientId}, ${AUTH_ENV_VARS.clientSecret}, etc.`,
                mode: 'env'
            });
        }

        const selection = await vscode.window.showQuickPick(items, {
            placeHolder: 'Select authentication method',
            ignoreFocusOut: true
        });

        return selection?.mode;
    }

    /**
     * Gets or prompts for the workspace URL.
     * First tries to fetch available workspaces and show a picker.
     * Falls back to manual input if fetching fails.
     * @param projectRoot - The project root path.
     * @param authMode - The authentication mode.
     * @param accessToken - Optional access token for fetching workspaces.
     * @returns The workspace URL, or undefined if cancelled.
     */
    private async getWorkspaceUrl(projectRoot: string, authMode: AuthMode, accessToken?: string): Promise<string | undefined> {
        // For env mode, try to get from environment variable first
        if (authMode === 'env') {
            const envUrl = this.authService.getWorkspaceUrlFromEnv();
            if (envUrl) {
                return envUrl;
            }
        }

        // Try to get cached URL for this project
        const cachedUrl = this.deployConfig.getWorkspaceUrl(projectRoot);

        // If we have an access token, try to fetch workspaces and show a picker
        if (accessToken) {
            try {
                this.outputChannel.appendLine('Fetching available workspaces...');
                const workspaces = await this.authService.getWorkspaces(accessToken);

                if (workspaces.length > 0) {
                    // Sort workspaces by name
                    workspaces.sort((a, b) => a.name.localeCompare(b.name));

                    // Build QuickPick items
                    const items: (vscode.QuickPickItem & { url?: string })[] = workspaces.map(ws => ({
                        label: ws.name,
                        description: ws.type,
                        detail: ws.description || `ID: ${ws.id}`,
                        url: `https://api.fabric.microsoft.com/v1/workspaces/${ws.id}`
                    }));

                    // Add option for manual entry
                    items.push({
                        label: '$(edit) Enter workspace URL manually',
                        description: 'Type the workspace URL or ID',
                        detail: 'Use this if your workspace is not in the list'
                    });

                    const selection = await vscode.window.showQuickPick(items, {
                        placeHolder: cachedUrl ? `Current: ${cachedUrl}` : 'Select a workspace to deploy to',
                        ignoreFocusOut: true
                    });

                    if (!selection) {
                        return undefined;
                    }

                    // If user selected manual entry, fall through to input box
                    if (selection.url) {
                        const workspaceUrl = selection.url;
                        await this.deployConfig.setWorkspaceUrl(projectRoot, workspaceUrl);
                        this.outputChannel.appendLine(`Selected workspace: ${selection.label}`);
                        return workspaceUrl;
                    }
                } else {
                    this.outputChannel.appendLine('No workspaces found. Falling back to manual input.');
                }
            } catch (error) {
                this.outputChannel.appendLine(`Failed to fetch workspaces: ${error}. Falling back to manual input.`);
            }
        }

        // Show input box with cached URL as default
        const workspaceUrl = await vscode.window.showInputBox({
            prompt: 'Enter the workspace URL or ID',
            placeHolder: 'https://api.fabric.microsoft.com/v1/workspaces/xxx',
            value: cachedUrl || '',
            ignoreFocusOut: true,
            validateInput: (value) => {
                if (!value || value.trim() === '') {
                    return 'Workspace URL is required';
                }
                return null;
            }
        });

        if (!workspaceUrl) {
            return undefined;
        }

        // Normalize the URL if it's just an ID
        let normalizedUrl = workspaceUrl.trim();
        if (!normalizedUrl.startsWith('http')) {
            // Assume it's a workspace ID
            normalizedUrl = `https://api.fabric.microsoft.com/v1/workspaces/${normalizedUrl}`;
        }

        // Cache the URL for this project
        await this.deployConfig.setWorkspaceUrl(projectRoot, normalizedUrl);

        return normalizedUrl;
    }

    /**
     * Prompts user for service principal credentials.
     * @returns The service principal credentials, or undefined if cancelled.
     */
    private async promptForServicePrincipalCredentials(): Promise<{ clientId: string; clientSecret: string; tenantId: string } | undefined> {
        const clientId = await vscode.window.showInputBox({
            prompt: 'Enter the Service Principal Client ID',
            placeHolder: 'Client ID',
            ignoreFocusOut: true
        });
        if (!clientId) { return undefined; }

        const clientSecret = await vscode.window.showInputBox({
            prompt: 'Enter the Service Principal Client Secret',
            placeHolder: 'Client Secret',
            password: true,
            ignoreFocusOut: true
        });
        if (!clientSecret) { return undefined; }

        const tenantId = await vscode.window.showInputBox({
            prompt: 'Enter the Tenant ID',
            placeHolder: 'Tenant ID',
            ignoreFocusOut: true
        });
        if (!tenantId) { return undefined; }

        return { clientId, clientSecret, tenantId };
    }

    /**
     * Shows a deployment preview and asks for confirmation.
     * @param projectRoot - The project root path.
     * @param workspaceUrl - The workspace URL.
     * @param authMode - The authentication mode.
     * @returns True if the user confirmed deployment.
     */
    private async showDeployPreview(
        projectRoot: string,
        workspaceUrl: string,
        authMode: AuthMode
    ): Promise<boolean> {
        const authLabel = authMode === 'interactive' ? 'Microsoft Account' :
                         authMode === 'service-principal' ? 'Service Principal' :
                         'Environment Variables';

        const selection = await vscode.window.showInformationMessage(
            `Deploy TMDL model to workspace?`,
            {
                modal: true,
                detail: `Project: ${projectRoot}\nWorkspace: ${workspaceUrl}\nAuth: ${authLabel}`
            },
            'Deploy',
            'Cancel'
        );

        return selection === 'Deploy';
    }

    /**
     * Handles the deployment result and displays appropriate messages.
     * @param result - The deployment result.
     */
    private handleDeployResult(result: DeployResult): void {
        if (result.isSuccess) {
            this.outputChannel.appendLine(`\n✓ ${result.message}`);

            if (result.changes && result.changes.length > 0) {
                this.outputChannel.appendLine('\nChanges:');
                result.changes.forEach(change => {
                    const icon = this.getChangeIcon(change.changeType);
                    this.outputChannel.appendLine(`  ${icon} ${change.objectType}: ${change.objectName}`);
                });
            }

            vscode.window.showInformationMessage(result.message);
        } else {
            this.outputChannel.appendLine(`\n✗ ${result.message}`);
            vscode.window.showErrorMessage(result.message);
        }
    }

    /**
     * Gets an icon representation for a change type.
     * @param changeType - The type of change.
     * @returns An icon string.
     */
    private getChangeIcon(changeType: string): string {
        switch (changeType.toLowerCase()) {
            case 'added':
                return '+';
            case 'removed':
                return '-';
            case 'modified':
                return '~';
            default:
                return '?';
        }
    }
}
