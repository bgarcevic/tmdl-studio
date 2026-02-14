import * as vscode from 'vscode';
import * as cp from 'child_process';
import { PathUtils } from './PathUtils';
import { ModelStructure } from '../views/explorer/ModelTreeItem';
import { AuthConfig } from '../types/auth';

/**
 * Client service for interacting with the TMDL CLI (timdle).
 * Wraps all CLI execution and output parsing logic.
 */
export class TimdleClient {
    private cliPath: string;

    /**
     * Creates a new TimdleClient instance.
     * @param context - The VS Code extension context for CLI path resolution.
     */
    constructor(private context: vscode.ExtensionContext) {
        this.cliPath = PathUtils.getCliPath(context);
    }

    /**
     * Validates the TMDL model at the specified path.
     * @param tmdlPath - The file system path to the TMDL folder.
     * @returns A promise that resolves to the validation output.
     */
    async validate(tmdlPath: string): Promise<string> {
        return new Promise((resolve, reject) => {
            cp.exec(`"${this.cliPath}" validate "${tmdlPath}"`, (err, stdout, stderr) => {
                if (err) {
                    reject(err);
                    return;
                }
                resolve(stdout);
            });
        });
    }

    /**
     * Retrieves the complete model structure from the TMDL CLI.
     * @param tmdlPath - The file system path to the TMDL folder.
     * @returns A promise that resolves to the model structure.
     */
    async getModelStructure(tmdlPath: string): Promise<ModelStructure> {
        return new Promise((resolve, reject) => {
            const command = `"${this.cliPath}" get-model-structure "${tmdlPath}"`;

            cp.exec(command, (err, stdout, stderr) => {
                if (err) {
                    reject(err);
                    return;
                }

                try {
                    const structure = JSON.parse(stdout) as ModelStructure;
                    resolve(structure);
                } catch (parseError) {
                    reject(parseError);
                }
            });
        });
    }

    /**
     * Lists tables in the TMDL model.
     * @param tmdlPath - The file system path to the TMDL folder.
     * @returns A promise that resolves to the table list output.
     */
    async listTables(tmdlPath: string): Promise<string> {
        return new Promise((resolve, reject) => {
            cp.exec(`"${this.cliPath}" list-tables "${tmdlPath}"`, (err, stdout, stderr) => {
                if (err) {
                    reject(err);
                    return;
                }
                resolve(stdout);
            });
        });
    }

    /**
     * Deploys the TMDL model to the specified workspace.
     * @param tmdlPath - The file system path to the TMDL folder.
     * @param authConfig - Authentication configuration including workspace URL and credentials.
     * @returns A promise that resolves to the deploy result.
     */
    async deploy(tmdlPath: string, authConfig: AuthConfig): Promise<DeployResult> {
        return new Promise((resolve, reject) => {
            // Pass auth config via environment variables to avoid exposing credentials in command line
            const authJson = JSON.stringify(authConfig);
            const env = {
                ...process.env,
                TMDL_AUTH_CONFIG: authJson
            };
            const command = `"${this.cliPath}" deploy "${tmdlPath}"`;

            cp.exec(command, { env }, (err, stdout, stderr) => {
                if (err) {
                    reject(err);
                    return;
                }

                try {
                    const result = JSON.parse(stdout) as DeployResult;
                    resolve(result);
                } catch (parseError) {
                    reject(parseError);
                }
            });
        });
    }
}

/**
 * Result of a deployment operation.
 */
export interface DeployResult {
    isSuccess: boolean;
    message: string;
}

// Re-export auth types for convenience
export { AuthConfig, AuthMode, ServicePrincipalCredentials } from '../types/auth';
