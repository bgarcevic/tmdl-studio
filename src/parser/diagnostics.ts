import * as vscode from 'vscode';
import { TmdlTokenizer } from './tokenizer';
import { TmdlValidator } from './validator';
import { ParserConfig, DEFAULT_CONFIG } from './types';

export class TmdlDiagnosticsProvider {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private config: ParserConfig;

    constructor(context: vscode.ExtensionContext, config: ParserConfig = DEFAULT_CONFIG) {
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('tmdl');
        this.config = config;
        context.subscriptions.push(this.diagnosticCollection);
    }

    /**
     * Update diagnostics for a TMDL document
     */
    public updateDiagnostics(document: vscode.TextDocument): void {
        // Only process .tmdl files
        if (document.languageId !== 'tmdl') {
            return;
        }

        // Clear existing diagnostics
        this.diagnosticCollection.delete(document.uri);

        // Parse and validate the document
        const tokenizer = new TmdlTokenizer(document.getText(), this.config);
        const tokens = tokenizer.tokenize();
        
        const validator = new TmdlValidator(tokens, this.config);
        const diagnostics = validator.validate();

        // Convert parser diagnostics to VSCode diagnostics
        const vsDiagnostics = diagnostics.map(d => {
            const range = new vscode.Range(
                d.line - 1,
                d.column - 1,
                d.line - 1,
                d.column - 1 + d.length
            );

            const diagnostic = new vscode.Diagnostic(
                range,
                d.message,
                this.getSeverity(d.severity)
            );
            
            diagnostic.source = 'tmdl';
            return diagnostic;
        });

        // Update diagnostics
        if (vsDiagnostics.length > 0) {
            this.diagnosticCollection.set(document.uri, vsDiagnostics);
        }
    }

    /**
     * Clear all diagnostics
     */
    public clearDiagnostics(): void {
        this.diagnosticCollection.clear();
    }

    private getSeverity(severity: string): vscode.DiagnosticSeverity {
        switch (severity) {
            case 'error':
                return vscode.DiagnosticSeverity.Error;
            case 'warning':
                return vscode.DiagnosticSeverity.Warning;
            case 'info':
                return vscode.DiagnosticSeverity.Information;
            default:
                return vscode.DiagnosticSeverity.Error;
        }
    }
}