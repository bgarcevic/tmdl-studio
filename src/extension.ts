import * as vscode from 'vscode';
import { TmdlDiagnosticsProvider } from './parser/diagnostics';
import { DEFAULT_CONFIG } from './parser/types';

let diagnosticsProvider: TmdlDiagnosticsProvider;

export function activate(context: vscode.ExtensionContext) {
    // Initialize diagnostics provider
    diagnosticsProvider = new TmdlDiagnosticsProvider(context, DEFAULT_CONFIG);

    // Register document formatter
    const formattingProvider = vscode.languages.registerDocumentFormattingEditProvider('tmdl', {
        provideDocumentFormattingEdits(_document: vscode.TextDocument): vscode.TextEdit[] {
            // Basic formatting placeholder - to be implemented
            return [];
        }
    });

    // Update diagnostics when document changes
    context.subscriptions.push(
        vscode.workspace.onDidChangeTextDocument(event => {
            if (event.document.languageId === 'tmdl') {
                diagnosticsProvider.updateDiagnostics(event.document);
            }
        })
    );

    // Update diagnostics when document opens
    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument(document => {
            if (document.languageId === 'tmdl') {
                diagnosticsProvider.updateDiagnostics(document);
            }
        })
    );

    // Update diagnostics for all open TMDL documents
    vscode.workspace.textDocuments.forEach(document => {
        if (document.languageId === 'tmdl') {
            diagnosticsProvider.updateDiagnostics(document);
        }
    });

    context.subscriptions.push(formattingProvider);
}

export function deactivate() {
    if (diagnosticsProvider) {
        diagnosticsProvider.clearDiagnostics();
    }
}
