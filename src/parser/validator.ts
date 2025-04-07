import { Token, TokenType, ParserDiagnostic, ParserConfig, DEFAULT_CONFIG } from './types';

export class TmdlValidator {
    private tokens: Token[];
    private diagnostics: ParserDiagnostic[] = [];
    private config: ParserConfig;
    private currentIndent: number = 0;
    private lastPropertyIndent: number = 0;

    constructor(tokens: Token[], config: ParserConfig = DEFAULT_CONFIG) {
        this.tokens = tokens;
        this.config = config;
    }

    /**
     * Validate the TMDL content based on tokens
     */
    public validate(): ParserDiagnostic[] {
        this.diagnostics = [];
        this.currentIndent = 0;
        this.lastPropertyIndent = 0;

        // Skip initial whitespace and comments
        let pos = 0;
        while (pos < this.tokens.length && 
               (this.tokens[pos].type === TokenType.Whitespace || 
                this.tokens[pos].type === TokenType.Comment || 
                this.tokens[pos].type === TokenType.LineBreak)) {
            pos++;
        }

        // Validate top-level structure
        while (pos < this.tokens.length) {
            pos = this.validateTopLevelDeclaration(pos);
        }

        return this.diagnostics;
    }

    private validateTopLevelDeclaration(startPos: number): number {
        this.currentIndent = 0;
        const token = this.tokens[startPos];

        if (token.type !== TokenType.Keyword) {
            this.addError(
                'Expected top-level declaration (database, model, table, relationship, or expression)',
                token
            );
            return this.skipToNextLine(startPos);
        }

        // Validate declaration name
        let pos = startPos + 1;
        if (pos >= this.tokens.length) {
            this.addError('Unexpected end of file after keyword', token);
            return this.tokens.length;
        }

        const nameToken = this.tokens[pos];
        if (nameToken.type !== TokenType.Identifier && nameToken.type !== TokenType.String) {
            this.addError('Expected identifier or string literal after keyword', nameToken);
            return this.skipToNextLine(pos);
        }

        // Move to next line and validate properties
        pos = this.skipToNextLine(pos);
        this.currentIndent = this.config.indentSize;
        return this.validateProperties(pos);
    }

    private validateProperties(startPos: number): number {
        let pos = startPos;
        let expectedIndent = this.currentIndent;

        while (pos < this.tokens.length) {
            const token = this.tokens[pos];
            
            // Skip blank lines and comments
            if (token.type === TokenType.LineBreak) {
                pos++;
                continue;
            }

            if (token.type === TokenType.Comment) {
                pos = this.skipToNextLine(pos);
                continue;
            }

            // Check indentation
            if (token.type === TokenType.Whitespace) {
                const actualIndent = token.value.length;
                if (actualIndent !== expectedIndent) {
                    this.addError(`Invalid indentation: expected ${expectedIndent} spaces, got ${actualIndent}`, token);
                }
                this.lastPropertyIndent = actualIndent;
                pos++;
                continue;
            }

            // If we find a non-indented token, we've reached the end of this block
            if (!this.hasExpectedIndentation(token, expectedIndent)) {
                return pos;
            }

            // Validate property format
            pos = this.validateProperty(pos);
        }

        return pos;
    }

    private validateProperty(startPos: number): number {
        const token = this.tokens[startPos];

        // Handle boolean properties (no value required)
        if (token.type === TokenType.Property) {
            return this.skipToNextLine(startPos);
        }

        if (token.type !== TokenType.Identifier) {
            this.addError('Expected property name', token);
            return this.skipToNextLine(startPos);
        }

        let pos = startPos + 1;
        if (pos >= this.tokens.length) {
            this.addError('Unexpected end of file in property declaration', token);
            return this.tokens.length;
        }

        // Skip whitespace between property name and delimiter
        while (pos < this.tokens.length && 
               this.tokens[pos].type === TokenType.Whitespace) {
            pos++;
        }

        if (pos >= this.tokens.length) {
            this.addError('Expected : or = after property name', token);
            return this.tokens.length;
        }

        const nextToken = this.tokens[pos];
        if (nextToken.type !== TokenType.Colon && nextToken.type !== TokenType.Equals) {
            this.addError('Expected : or = after property name', nextToken);
            return this.skipToNextLine(pos);
        }

        // Skip delimiter
        pos++;

        // Skip whitespace after delimiter
        while (pos < this.tokens.length && 
               this.tokens[pos].type === TokenType.Whitespace) {
            pos++;
        }

        // Validate property value
        if (pos >= this.tokens.length) {
            this.addError('Expected property value', nextToken);
            return this.tokens.length;
        }

        // Handle multi-line expressions
        if (nextToken.type === TokenType.Equals) {
            return this.validateExpression(pos);
        }

        // Handle single-line property values
        const valueToken = this.tokens[pos];
        if (valueToken.type !== TokenType.String && 
            valueToken.type !== TokenType.Identifier) {
            this.addError('Invalid property value', valueToken);
        }

        return this.skipToNextLine(pos);
    }

    private validateExpression(startPos: number): number {
        let pos = startPos;
        const expressionIndent = this.lastPropertyIndent + this.config.indentSize;

        while (pos < this.tokens.length) {
            const token = this.tokens[pos];

            if (token.type === TokenType.LineBreak) {
                pos++;
                continue;
            }

            if (token.type === TokenType.Whitespace) {
                if (token.value.length !== expressionIndent) {
                    this.addError(`Invalid expression indentation: expected ${expressionIndent} spaces`, token);
                }
                pos++;
                continue;
            }

            // End of expression when we find a token with less indentation
            if (!this.hasExpectedIndentation(token, this.lastPropertyIndent)) {
                return pos;
            }

            pos++;
        }

        return pos;
    }

    private hasExpectedIndentation(token: Token, expected: number): boolean {
        if (token.type === TokenType.Whitespace) {
            return token.value.length === expected;
        }
        return token.indent * this.config.indentSize === expected;
    }

    private skipToNextLine(pos: number): number {
        while (pos < this.tokens.length && this.tokens[pos].type !== TokenType.LineBreak) {
            pos++;
        }
        return pos + 1;
    }

    private addError(message: string, token: Token): void {
        this.diagnostics.push({
            message,
            severity: 'error',
            line: token.line,
            column: token.column,
            length: token.length
        });
    }
}