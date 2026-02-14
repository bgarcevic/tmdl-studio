import * as vscode from 'vscode';
import * as crypto from 'crypto';

/**
 * Configuration service for deployment settings.
 * Manages workspace URLs per project.
 */
export class DeployConfig {
    private static readonly WORKSPACE_URL_PREFIX = 'tmdl-workspace-url-';

    /**
     * Creates a new DeployConfig instance.
     * @param context - The VS Code extension context.
     */
    constructor(private context: vscode.ExtensionContext) {}

    /**
     * Gets the workspace URL for a project.
     * @param projectPath - The path to the project root.
     * @returns The workspace URL if cached, undefined otherwise.
     */
    getWorkspaceUrl(projectPath: string): string | undefined {
        const key = this.getStorageKey(projectPath);
        return this.context.workspaceState.get<string>(key);
    }

    /**
     * Sets the workspace URL for a project.
     * @param projectPath - The path to the project root.
     * @param url - The workspace URL.
     */
    setWorkspaceUrl(projectPath: string, url: string): Thenable<void> {
        const key = this.getStorageKey(projectPath);
        return this.context.workspaceState.update(key, url);
    }

    /**
     * Generates a storage key for a project path.
     * Uses a hash to keep keys short and filesystem-safe.
     * @param projectPath - The project path.
     * @returns The storage key.
     */
    private getStorageKey(projectPath: string): string {
        const hash = crypto.createHash('sha256').update(projectPath).digest('hex').substring(0, 16);
        return `${DeployConfig.WORKSPACE_URL_PREFIX}${hash}`;
    }
}
