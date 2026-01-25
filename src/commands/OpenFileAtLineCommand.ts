import * as vscode from 'vscode';

export class OpenFileAtLineCommand {
    static register(): vscode.Disposable {
        return vscode.commands.registerCommand('tmdl-studio.open-file-at-line', async (filePath: string, lineNumber: number | undefined) => {
            const uri = vscode.Uri.file(filePath);
            const options = (lineNumber !== undefined && lineNumber > 0) ? {
                selection: new vscode.Range(
                    new vscode.Position(lineNumber - 1, 0),
                    new vscode.Position(lineNumber - 1, 0)
                )
            } : undefined;
            await vscode.window.showTextDocument(uri, options);
        });
    }
}
