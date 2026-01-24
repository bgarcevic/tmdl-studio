import * as vscode from 'vscode';
import { TabularTreeProvider } from './views/explorer/TabularTreeProvider';
import { ValidateCommand } from './commands/ValidateCommand';
import { CloseModelCommand } from './commands/CloseModelCommand';

/**
 * Activates the TMDL Studio extension.
 * @param context - The extension context.
 */
export function activate(context: vscode.ExtensionContext) {
    console.log('TMDL Studio is active!');

    const treeProvider = new TabularTreeProvider(context);
    vscode.window.registerTreeDataProvider('tabular-model-explorer', treeProvider);

    treeProvider.loadState();

    const selectFolderCommand = vscode.commands.registerCommand('tmdl-studio.select-folder', async () => {
        const uri = await vscode.window.showOpenDialog({
            canSelectFolders: true,
            canSelectFiles: true,
            canSelectMany: false,
            title: 'Select TMDL Model Folder or File'
        });

        if (uri && uri[0]) {
            const path = uri[0].fsPath;
            try {
                await treeProvider.setTmdlFolder(path);
            } catch (error) {
                vscode.window.showErrorMessage(error instanceof Error ? error.message : String(error));
            }
        }
    });

    const validateCommand = ValidateCommand.register(context);
    const closeModelCommand = CloseModelCommand.register(context, treeProvider);

    context.subscriptions.push(selectFolderCommand);
    context.subscriptions.push(validateCommand);
    context.subscriptions.push(closeModelCommand);
}

/**
 * Deactivates the TMDL Studio extension.
 */
export function deactivate() {}
