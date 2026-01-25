import * as vscode from 'vscode';
import * as path from 'path';
import { TabularTreeProvider } from '../views/explorer/TabularTreeProvider';
import { ProjectRootDetector } from '../utils/ProjectRootDetector';

/**
 * Listener for detecting and prompting to load TMDL projects when files are opened.
 */
export class FileOpenListener {
    /**
     * Registers the file open listener with VS Code.
     * @param context - The extension context.
     * @param treeProvider - The tabular tree provider instance.
     * @returns The disposable listener registration.
     */
    static register(context: vscode.ExtensionContext, treeProvider: TabularTreeProvider): vscode.Disposable {
        const openedFiles = new Set<string>();

        return vscode.workspace.onDidOpenTextDocument(async (document) => {
            const filePath = document.uri.fsPath;
            const extension = path.extname(filePath);

            if (extension !== '.tmdl' && extension !== '.pbism' && path.basename(filePath) !== '.platform') {
                return;
            }

            if (openedFiles.has(filePath)) {
                return;
            }

            openedFiles.add(filePath);

            const projectRoot = ProjectRootDetector.detectProjectRoot(filePath);
            if (!projectRoot) {
                return;
            }

            const currentFolder = treeProvider.getCurrentFolder();
            if (currentFolder === projectRoot) {
                return;
            }

            const fileName = path.basename(filePath);
            const response = await vscode.window.showInformationMessage(
                `Detected TMDL project from ${fileName}. Load this project?`,
                'Yes',
                'No'
            );

            if (response === 'Yes') {
                try {
                    await treeProvider.setTmdlFolder(filePath);
                } catch (error) {
                    vscode.window.showErrorMessage(error instanceof Error ? error.message : String(error));
                }
            }
        });
    }
}
