import * as vscode from 'vscode';
import * as path from 'path';
import { TabularTreeProvider } from './views/explorer/TabularTreeProvider';
import { ValidateCommand } from './commands/ValidateCommand';
import { CloseModelCommand } from './commands/CloseModelCommand';
import { ProjectRootDetector } from './utils/ProjectRootDetector';

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

    const openedFiles = new Set<string>();

    const fileOpenListener = vscode.workspace.onDidOpenTextDocument(async (document) => {
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

    context.subscriptions.push(selectFolderCommand);
    context.subscriptions.push(validateCommand);
    context.subscriptions.push(closeModelCommand);
    context.subscriptions.push(fileOpenListener);
}

/**
 * Deactivates the TMDL Studio extension.
 */
export function deactivate() {}
