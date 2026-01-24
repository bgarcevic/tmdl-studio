import * as vscode from 'vscode';
import * as cp from 'child_process';
import { PathUtils } from './PathUtils';
import { ModelStructure } from '../views/explorer/ModelTreeItem';

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
            cp.exec(`"${this.cliPath}" get-model-structure "${tmdlPath}"`, (err, stdout, stderr) => {
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
}
