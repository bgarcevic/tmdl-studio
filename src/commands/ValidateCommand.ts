import * as vscode from 'vscode';
import { TimdleClient } from '../cli/TimdleClient';

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
        const outputChannel = vscode.window.createOutputChannel('TMDL Studio');
        outputChannel.show();
        outputChannel.appendLine(`Running validation on: ${rootPath}...`);

        try {
            const output = await this.cliClient.validate(rootPath);
            outputChannel.appendLine(output);
        } catch (error) {
            outputChannel.appendLine(`Error executing CLI: ${error}`);
        }
    }
}
