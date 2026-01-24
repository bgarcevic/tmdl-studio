import * as vscode from 'vscode';

/**
 * Configuration section name for TMDL Studio settings.
 */
const CONFIG_SECTION = 'tmdl-studio';

/**
 * Helper class for accessing TMDL Studio configuration settings.
 */
export class Config {
    /**
     * Gets the current configuration for TMDL Studio.
     * @returns The VS Code configuration object.
     */
    static get(): vscode.WorkspaceConfiguration {
        return vscode.workspace.getConfiguration(CONFIG_SECTION);
    }

    /**
     * Gets a specific configuration value.
     * @param key - The configuration key.
     * @returns The configuration value or undefined.
     */
    static getValue<T>(key: string): T | undefined {
        return this.get().get<T>(key);
    }

    /**
     * Sets a specific configuration value.
     * @param key - The configuration key.
     * @param value - The configuration value.
     * @param target - The configuration target (global or workspace).
     * @returns A promise that resolves when the configuration is updated.
     */
    static async set<T>(
        key: string,
        value: T,
        target: vscode.ConfigurationTarget = vscode.ConfigurationTarget.Global
    ): Promise<void> {
        await this.get().update(key, value, target);
    }
}
