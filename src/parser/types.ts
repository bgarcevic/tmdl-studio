/**
 * Types of tokens that can be found in TMDL files
 */
export enum TokenType {
    Whitespace = 'Whitespace',
    Identifier = 'Identifier',
    Keyword = 'Keyword',
    Property = 'Property',
    Equals = 'Equals',
    Colon = 'Colon',
    String = 'String',
    Comment = 'Comment',
    LineBreak = 'LineBreak',
    Invalid = 'Invalid'
}

/**
 * Represents a token from the TMDL file
 */
export interface Token {
    type: TokenType;
    value: string;
    line: number;
    column: number;
    length: number;
    indent: number;
}

/**
 * Represents a diagnostic message from the parser
 */
export interface ParserDiagnostic {
    message: string;
    severity: 'error' | 'warning' | 'info';
    line: number;
    column: number;
    length: number;
}

/**
 * TMDL language keywords
 */
export const Keywords = [
    'database',
    'model',
    'table',
    'relationship',
    'expression',
    'partition',
    'measure',
    'column',
    'role'
] as const;

export type Keyword = typeof Keywords[number];

/**
 * Properties that can be boolean (no value required)
 */
export const BooleanProperties = [
    'isHidden',
    'isKey',
    'isNullable',
    'isUnique',
    'isActive',
    'isDefault'
] as const;

/**
 * Configuration for the TMDL parser
 */
export interface ParserConfig {
    indentSize: number;
    maxLineLength: number;
}

/**
 * Default parser configuration
 */
export const DEFAULT_CONFIG: ParserConfig = {
    indentSize: 4,
    maxLineLength: 120
};