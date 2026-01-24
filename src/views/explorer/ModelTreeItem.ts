import * as vscode from 'vscode';
import * as path from 'path';

/**
 * Represents the complete structure of a TMDL model.
 */
export interface ModelStructure {
    name: string;
    path: string;
    database: DatabaseInfo;
    model: ModelInfo;
    tables: TableNode[];
    relationships: RelationshipNode[];
    expressions: ExpressionNode[];
    cultures: CultureNode[];
}

/** Database information. */
export interface DatabaseInfo {
    name: string;
    file: string;
}

/** Model information. */
export interface ModelInfo {
    name: string;
    file: string;
}

/** Table node containing columns, measures, and partitions. */
export interface TableNode {
    name: string;
    file: string;
    columns: ColumnNode[];
    measures: MeasureNode[];
    partitions: PartitionNode[];
}

/** Column node with data type and visibility. */
export interface ColumnNode {
    name: string;
    dataType?: string;
    isHidden?: boolean;
}

/** Measure node with format string. */
export interface MeasureNode {
    name: string;
    formatString?: string;
}

/** Partition node with processing mode. */
export interface PartitionNode {
    name: string;
    mode?: string;
}

/** Relationship node between tables. */
export interface RelationshipNode {
    id: string;
    name: string;
    file: string;
    fromColumn: string;
    toColumn: string;
}

/** Expression node (calculation items, M expressions, etc.). */
export interface ExpressionNode {
    name: string;
    file: string;
    kind: string;
}

/** Culture/translation node. */
export interface CultureNode {
    name: string;
    file: string;
}

/**
 * Union type for all possible tree nodes in the Tabular Explorer.
 */
export type TreeNode =
    | { type: 'no-folder' }
    | { type: 'loading' }
    | { type: 'error'; message: string }
    | { type: 'database'; data: DatabaseInfo }
    | { type: 'model'; data: ModelInfo }
    | { type: 'tables' }
    | { type: 'table'; data: TableNode }
    | { type: 'columns'; parentTable: string }
    | { type: 'column'; data: ColumnNode; parentTable: string }
    | { type: 'measures'; parentTable: string }
    | { type: 'measure'; data: MeasureNode; parentTable: string }
    | { type: 'partitions'; parentTable: string }
    | { type: 'partition'; data: PartitionNode; parentTable: string }
    | { type: 'relationships' }
    | { type: 'relationship'; data: RelationshipNode }
    | { type: 'expressions' }
    | { type: 'expression'; data: ExpressionNode }
    | { type: 'cultures' }
    | { type: 'culture'; data: CultureNode };

/**
 * Creates a VS Code TreeItem from a TreeNode.
 * @param element - The tree node to convert.
 * @param folderPath - The TMDL folder path for resolving file paths.
 * @param modelData - The model structure data for resolving child files.
 * @returns The TreeItem for display.
 */
export function createTreeItem(
    element: TreeNode,
    folderPath: string,
    modelData: ModelStructure | undefined
): vscode.TreeItem {
    const label = getLabel(element);
    const collapsibleState = getCollapsibleState(element);
    const iconPath = getIcon(element.type);

    const item = new vscode.TreeItem(label, collapsibleState);
    item.iconPath = iconPath;
    item.contextValue = element.type;

    const command = getOpenCommand(element, folderPath, modelData);
    if (command) {
        item.command = command;
    }

    const tooltip = getTooltip(element);
    if (tooltip) {
        item.tooltip = tooltip;
    }

    return item;
}

/**
 * Gets the display label for a tree node.
 * @param element - The tree node.
 * @returns The display label string.
 */
function getLabel(element: TreeNode): string {
    switch (element.type) {
        case 'no-folder': return 'No TMDL folder selected. Click the folder icon to select one.';
        case 'loading': return 'Loading model...';
        case 'error': return element.message;
        case 'database': return element.data.name;
        case 'model': return element.data.name;
        case 'tables': return 'Tables';
        case 'table': return element.data.name;
        case 'columns': return 'Columns';
        case 'column': return element.data.name;
        case 'measures': return 'Measures';
        case 'measure': return element.data.name;
        case 'partitions': return 'Partitions';
        case 'partition': return element.data.name;
        case 'relationships': return 'Relationships';
        case 'relationship': return element.data.name;
        case 'expressions': return 'Expressions';
        case 'expression': return element.data.name;
        case 'cultures': return 'Cultures';
        case 'culture': return element.data.name;
        default: return '';
    }
}

/**
 * Gets the collapsible state for a tree node.
 * @param element - The tree node.
 * @returns The collapsible state.
 */
function getCollapsibleState(element: TreeNode): vscode.TreeItemCollapsibleState {
    switch (element.type) {
        case 'no-folder':
        case 'loading':
        case 'error':
        case 'column':
        case 'measure':
        case 'partition':
        case 'relationship':
        case 'expression':
        case 'culture':
            return vscode.TreeItemCollapsibleState.None;
        default:
            return vscode.TreeItemCollapsibleState.Collapsed;
    }
}

/**
 * Gets the icon for a tree node type.
 * @param type - The tree node type.
 * @returns The theme icon.
 */
function getIcon(type: string): vscode.ThemeIcon {
    switch (type) {
        case 'database': return new vscode.ThemeIcon('database');
        case 'model': return new vscode.ThemeIcon('symbol-namespace');
        case 'tables': return new vscode.ThemeIcon('folder');
        case 'table': return new vscode.ThemeIcon('table');
        case 'columns': return new vscode.ThemeIcon('folder');
        case 'column': return new vscode.ThemeIcon('symbol-field');
        case 'measures': return new vscode.ThemeIcon('folder');
        case 'measure': return new vscode.ThemeIcon('symbol-function');
        case 'partitions': return new vscode.ThemeIcon('folder');
        case 'partition': return new vscode.ThemeIcon('symbol-array');
        case 'relationships': return new vscode.ThemeIcon('folder');
        case 'relationship': return new vscode.ThemeIcon('link');
        case 'expressions': return new vscode.ThemeIcon('folder');
        case 'expression': return new vscode.ThemeIcon('variable');
        case 'cultures': return new vscode.ThemeIcon('folder');
        case 'culture': return new vscode.ThemeIcon('file');
        default: return new vscode.ThemeIcon('circle-outline');
    }
}

/**
 * Gets the tooltip for a tree node.
 * @param element - The tree node.
 * @returns The tooltip string or undefined.
 */
function getTooltip(element: TreeNode): string | undefined {
    switch (element.type) {
        case 'column':
            return `${element.data.name} (${element.data.dataType || 'Unknown'})${element.data.isHidden ? ' (Hidden)' : ''}`;
        case 'measure':
            return `${element.data.name}${element.data.formatString ? ` [${element.data.formatString}]` : ''}`;
        case 'partition':
            return `${element.data.name} (${element.data.mode || 'Unknown'})`;
        case 'expression':
            return `${element.data.name} (${element.data.kind})`;
        case 'relationship':
            return `${element.data.fromColumn} → ${element.data.toColumn}`;
        default:
            return undefined;
    }
}

/**
 * Gets the command to open the associated file for a tree node.
 * @param element - The tree node.
 * @param folderPath - The TMDL folder path.
 * @param modelData - The model structure data.
 * @returns The VS Code command or undefined.
 */
function getOpenCommand(
    element: TreeNode,
    folderPath: string,
    modelData: ModelStructure | undefined
): vscode.Command | undefined {
    let relativePath: string | undefined;

    switch (element.type) {
        case 'database':
            relativePath = element.data.file;
            break;
        case 'model':
            relativePath = element.data.file;
            break;
        case 'table':
            relativePath = element.data.file;
            break;
        case 'column':
        case 'measure':
        case 'partition': {
            const table = modelData?.tables.find(t => t.name === element.parentTable);
            relativePath = table?.file;
            break;
        }
        case 'relationship':
            relativePath = element.data.file;
            break;
        case 'expression':
            relativePath = element.data.file;
            break;
        case 'culture':
            relativePath = element.data.file;
            break;
    }

    if (!relativePath) {return undefined;}

    const fullPath = path.join(folderPath, relativePath);
    const uri = vscode.Uri.file(fullPath);

    return {
        command: 'vscode.open',
        arguments: [uri],
        title: 'Open File'
    };
}
