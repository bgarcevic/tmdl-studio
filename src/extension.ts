import * as vscode from 'vscode';
import { TabularTreeProvider } from './views/explorer/TabularTreeProvider';
import { ValidateCommand } from './commands/ValidateCommand';
import { CloseModelCommand } from './commands/CloseModelCommand';
import { SelectFolderCommand } from './commands/SelectFolderCommand';
import { OpenFileAtLineCommand } from './commands/OpenFileAtLineCommand';
import { FileOpenListener } from './listeners/FileOpenListener';
import { FileSaveListener } from './listeners/FileSaveListener';

/**
 * Activates the TMDL Studio extension.
 * @param context - The extension context.
 */
export function activate(context: vscode.ExtensionContext) {
    console.log('TMDL Studio is active!');

    const treeProvider = new TabularTreeProvider(context);
    vscode.window.registerTreeDataProvider('tabular-model-explorer', treeProvider);

    treeProvider.loadState();

    const selectFolderCommand = SelectFolderCommand.register(context, treeProvider);
    const validateCommand = ValidateCommand.register(context);
    const closeModelCommand = CloseModelCommand.register(context, treeProvider);
    const openFileAtLineCommand = OpenFileAtLineCommand.register();
    const fileOpenListener = FileOpenListener.register(context, treeProvider);
    const fileSaveListener = FileSaveListener.register(context, treeProvider);

    context.subscriptions.push(selectFolderCommand);
    context.subscriptions.push(validateCommand);
    context.subscriptions.push(closeModelCommand);
    context.subscriptions.push(openFileAtLineCommand);
    context.subscriptions.push(fileOpenListener);
    context.subscriptions.push(fileSaveListener);
}

/**
 * Deactivates the TMDL Studio extension.
 */
export function deactivate() {}
