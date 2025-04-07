import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    const disposable = vscode.languages.registerDocumentFormattingEditProvider('tmdl', {
        provideDocumentFormattingEdits(document: vscode.TextDocument): vscode.TextEdit[] {
            // Basic formatting placeholder
            return [];
        }
    });

    context.subscriptions.push(disposable);
}

export function deactivate() {}
