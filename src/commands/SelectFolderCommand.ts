import * as vscode from 'vscode';
import { TabularTreeProvider } from '../views/explorer/TabularTreeProvider';

export class SelectFolderCommand {
    static register(context: vscode.ExtensionContext, treeProvider: TabularTreeProvider): vscode.Disposable {
        return vscode.commands.registerCommand('tmdl-studio.select-folder', async () => {
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
    }
}
