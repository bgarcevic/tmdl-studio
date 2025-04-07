import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    console.log('TMDL Studio extension is now active');

    // Register language features
    const disposable = vscode.languages.registerDocumentFormattingEditProvider('tmdl', {
        provideDocumentFormattingEdits(document: vscode.TextDocument): vscode.TextEdit[] {
            // Basic formatting placeholder
            return [];
        }
    });

    context.subscriptions.push(disposable);
}

export function deactivate() {}
