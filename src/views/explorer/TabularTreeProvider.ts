import * as vscode from 'vscode';
import { TimdleClient } from '../../cli/TimdleClient';
import { ModelStructure, TreeNode, createTreeItem } from './ModelTreeItem';

/**
 * Tree data provider for the Tabular Model Explorer view.
 */
export class TabularTreeProvider implements vscode.TreeDataProvider<TreeNode> {
    private _onDidChangeTreeData = new vscode.EventEmitter<TreeNode | undefined | null | void>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private currentTmdlFolder: string | undefined;
    private modelData: ModelStructure | undefined;
    private cliClient: TimdleClient;

    /**
     * @param context - The extension context.
     */
    constructor(private context: vscode.ExtensionContext) {
        this.cliClient = new TimdleClient(context);
    }

    /**
     * Loads the saved TMDL folder from global state.
     */
    async loadState(): Promise<void> {
        const savedFolder = this.context.globalState.get<string>('tmdlFolder');
        if (savedFolder) {
            this.currentTmdlFolder = savedFolder;
            await this.loadModel();
            await vscode.commands.executeCommand('setContext', 'tmdlModelOpen', true);
        }
    }

    /**
     * Sets the TMDL folder path and loads the model.
     * @param folderPath - The file system path to the TMDL folder.
     */
    async setTmdlFolder(folderPath: string): Promise<void> {
        this.currentTmdlFolder = folderPath;
        await this.context.globalState.update('tmdlFolder', folderPath);
        await this.loadModel();
        await vscode.commands.executeCommand('setContext', 'tmdlModelOpen', true);
        this.refresh();
    }

    /**
     * Closes the currently open TMDL model.
     */
    async closeTmdlFolder(): Promise<void> {
        this.currentTmdlFolder = undefined;
        this.modelData = undefined;
        await this.context.globalState.update('tmdlFolder', undefined);
        await vscode.commands.executeCommand('setContext', 'tmdlModelOpen', false);
        this.refresh();
    }

    /**
     * Refreshes the tree view.
     */
    refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    /**
     * Returns the UI representation (TreeItem) for a tree node.
     * @param element - The tree node to convert.
     * @returns The TreeItem for display.
     */
    getTreeItem(element: TreeNode): vscode.TreeItem {
        return createTreeItem(element, this.currentTmdlFolder || '', this.modelData);
    }

    /**
     * Gets the child nodes for a tree node.
     * @param element - The parent tree node (undefined for root).
     * @returns The child nodes.
     */
    async getChildren(element?: TreeNode): Promise<TreeNode[]> {
        if (!element) {
            return this.getRootChildren();
        }

        return this.getChildElements(element);
    }

    /**
     * Gets the root-level children of the tree.
     * @returns The root tree nodes.
     */
    private async getRootChildren(): Promise<TreeNode[]> {
        if (!this.currentTmdlFolder) {
            return [{ type: 'no-folder' }];
        }

        if (!this.modelData) {
            await this.loadModel();
            if (!this.modelData) {
                return [{ type: 'error', message: 'Failed to load model' }];
            }
        }

        return [
            { type: 'database', data: this.modelData.database },
            { type: 'model', data: this.modelData.model },
            { type: 'tables' },
            { type: 'relationships' },
            { type: 'expressions' },
            { type: 'cultures' }
        ];
    }

    /**
     * Gets the child elements for a specific tree node.
     * @param element - The parent tree node.
     * @returns The child tree nodes.
     */
    private getChildElements(element: TreeNode): TreeNode[] {
        if (!this.modelData) {return [];}

        switch (element.type) {
            case 'tables':
                return this.modelData.tables.map(t => ({ type: 'table', data: t }));
            case 'table':
                return [
                    { type: 'columns', parentTable: element.data.name },
                    { type: 'measures', parentTable: element.data.name },
                    { type: 'partitions', parentTable: element.data.name }
                ];
            case 'columns':
                const columnsTable = this.modelData.tables.find(t => t.name === element.parentTable);
                return columnsTable?.columns.map(c => ({ type: 'column', data: c, parentTable: element.parentTable })) || [];
            case 'measures':
                const measuresTable = this.modelData.tables.find(t => t.name === element.parentTable);
                return measuresTable?.measures.map(m => ({ type: 'measure', data: m, parentTable: element.parentTable })) || [];
            case 'partitions':
                const partitionsTable = this.modelData.tables.find(t => t.name === element.parentTable);
                return partitionsTable?.partitions.map(p => ({ type: 'partition', data: p, parentTable: element.parentTable })) || [];
            case 'relationships':
                return this.modelData.relationships.map(r => ({ type: 'relationship', data: r }));
            case 'expressions':
                return this.modelData.expressions.map(e => ({ type: 'expression', data: e }));
            case 'cultures':
                return this.modelData.cultures.map(c => ({ type: 'culture', data: c }));
            default:
                return [];
        }
    }

    /**
     * Loads the TMDL model structure from the current folder.
     */
    private async loadModel(): Promise<void> {
        if (!this.currentTmdlFolder) {return;}

        try {
            this.modelData = await this.cliClient.getModelStructure(this.currentTmdlFolder);
        } catch (error) {
            this.modelData = undefined;
            vscode.window.showErrorMessage(`Failed to load TMDL model: ${error}`);
        }
    }
}
