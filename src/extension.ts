import * as vscode from 'vscode';
import { TabularTreeProvider } from './views/explorer/TabularTreeProvider';
import { ValidateCommand } from './commands/ValidateCommand';

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
        const folderUri = await vscode.window.showOpenDialog({
            canSelectFolders: true,
            canSelectFiles: false,
            canSelectMany: false,
            title: 'Select TMDL Model Folder'
        });

        if (folderUri && folderUri[0]) {
            await treeProvider.setTmdlFolder(folderUri[0].fsPath);
        }
    });

    const validateCommand = ValidateCommand.register(context);

    context.subscriptions.push(selectFolderCommand);
    context.subscriptions.push(validateCommand);
}

/**
 * Deactivates the TMDL Studio extension.
 */
export function deactivate() {}
