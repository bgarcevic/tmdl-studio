import * as vscode from 'vscode';
import { TimdleClient } from '../cli/TimdleClient';
import { ProjectRootDetector } from '../utils/ProjectRootDetector';

/**
 * Command handler for validating TMDL models.
 */
export class ValidateCommand {
    private cliClient: TimdleClient;

    /**
     * Creates a new ValidateCommand instance.
     * @param context - The VS Code extension context.
     */
    constructor(private context: vscode.ExtensionContext) {
        this.cliClient = new TimdleClient(context);
    }

    /**
     * Registers the validate command with VS Code.
     * @param context - The VS Code extension context.
     * @returns The disposable command registration.
     */
    static register(context: vscode.ExtensionContext): vscode.Disposable {
        const command = new ValidateCommand(context);
        return vscode.commands.registerCommand('tmdl-studio.validate', () => command.execute());
    }

    /**
     * Executes the validate command.
     */
    private async execute(): Promise<void> {
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            vscode.window.showErrorMessage('Please open a folder containing TMDL files.');
            return;
        }

        const rootPath = workspaceFolders[0].uri.fsPath;
        const projectRoot = ProjectRootDetector.detectProjectRoot(rootPath);

        if (!projectRoot) {
            vscode.window.showErrorMessage('Could not detect TMDL project root. Make sure the folder contains definition.pbism, .platform, or definition folder.');
            return;
        }

        const outputChannel = vscode.window.createOutputChannel('TMDL Studio');
        outputChannel.show();
        outputChannel.appendLine(`Running validation on: ${projectRoot}...`);

        try {
            const output = await this.cliClient.validate(projectRoot);
            outputChannel.appendLine(output);
        } catch (error) {
            outputChannel.appendLine(`Error executing CLI: ${error}`);
        }
    }
}
