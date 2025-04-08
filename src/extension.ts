import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
    // Register document formatter
    const formattingProvider = vscode.languages.registerDocumentFormattingEditProvider('tmdl', {
        provideDocumentFormattingEdits(_document: vscode.TextDocument): vscode.TextEdit[] {
            // Basic formatting placeholder - to be implemented
            return [];
        }
    });

    context.subscriptions.push(formattingProvider);
}

export function deactivate() {
    // Cleanup when extension is deactivated
}
