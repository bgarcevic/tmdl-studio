import * as vscode from 'vscode';
import * as path from 'path';
import { TabularTreeProvider } from '../views/explorer/TabularTreeProvider';
import { ProjectRootDetector } from '../utils/ProjectRootDetector';

/**
 * Listener for reloading the TMDL model when files in the project are saved.
 */
export class FileSaveListener {
    /**
     * Registers the file save listener with VS Code.
     * @param treeProvider - The tabular tree provider instance.
     * @returns The disposable listener registration.
     */
    static register(treeProvider: TabularTreeProvider): vscode.Disposable {
        return vscode.workspace.onDidSaveTextDocument(async (document) => {
            const filePath = document.uri.fsPath;
            const extension = path.extname(filePath);

            if (extension !== '.tmdl' && extension !== '.pbism' && path.basename(filePath) !== '.platform') {
                return;
            }

            const currentFolder = treeProvider.getCurrentFolder();
            if (!currentFolder) {
                return;
            }

            const savedFileProjectRoot = ProjectRootDetector.detectProjectRoot(filePath);
            if (!savedFileProjectRoot) {
                return;
            }

            if (path.relative(currentFolder, savedFileProjectRoot) !== '') {
                return;
            }

            try {
                await treeProvider.reload();
            } catch (error) {
                console.error('Failed to reload model after save:', error);
            }
        });
    }
}
