import * as vscode from 'vscode';
import { TabularTreeProvider } from '../views/explorer/TabularTreeProvider';

/**
 * Command handler for closing the currently open TMDL model.
 */
export class CloseModelCommand {
    /**
     * Registers the close model command with VS Code.
     * @param context - The extension context.
     * @param treeProvider - The tabular tree provider instance.
     * @returns The disposable command registration.
     */
    static register(context: vscode.ExtensionContext, treeProvider: TabularTreeProvider): vscode.Disposable {
        return vscode.commands.registerCommand('tmdl-studio.close-model', async () => {
            await treeProvider.closeTmdlFolder();
        });
    }
}
