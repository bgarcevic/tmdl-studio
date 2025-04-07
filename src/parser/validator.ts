import { Token, TokenType, ParserDiagnostic, ParserConfig, DEFAULT_CONFIG } from './types';

export class TmdlValidator {
    private tokens: Token[];
    private diagnostics: ParserDiagnostic[] = [];
    private config: ParserConfig;
    private expectedIndent: number = 0;

    constructor(tokens: Token[], config: ParserConfig = DEFAULT_CONFIG) {
        this.tokens = tokens;
        this.config = config;
    }

    /**
     * Validate the TMDL content based on tokens
     */
    public validate(): ParserDiagnostic[] {
        this.diagnostics = [];
        this.expectedIndent = 0;

        let pos = 0;
        while (pos < this.tokens.length) {
            const token = this.tokens[pos];

            if (token.type === TokenType.LineBreak || 
                token.type === TokenType.Whitespace ||
                token.type === TokenType.Comment) {
                pos++;
                continue;
            }

            if (token.type !== TokenType.Keyword) {
                this.addError(
                    'Expected top-level declaration (database, model, table, relationship, or expression)',
                    token
                );
                pos = this.skipToNextLine(pos);
                continue;
            }

            pos = this.validateTopLevelDeclaration(pos);
        }

        return this.diagnostics;
    }

    private validateTopLevelDeclaration(startPos: number): number {
        const token = this.tokens[startPos];
        let pos = startPos + 1;

        // Skip whitespace after keyword
        while (pos < this.tokens.length && 
               this.tokens[pos].type === TokenType.Whitespace) {
            pos++;
        }

        // Validate identifier after keyword
        if (pos >= this.tokens.length || 
            (this.tokens[pos].type !== TokenType.Identifier && 
             this.tokens[pos].type !== TokenType.String)) {
            this.addError('Expected identifier or string literal after keyword', token);
            return this.skipToNextLine(startPos);
        }

        pos++;

        // Find the start of the next line
        while (pos < this.tokens.length && 
               this.tokens[pos].type !== TokenType.LineBreak) {
            pos++;
        }
        pos++;

        // Validate the block of properties
        this.expectedIndent = this.config.indentSize;
        return this.validateBlock(pos);
    }

    private validateBlock(startPos: number): number {
        let pos = startPos;
        const blockIndent = this.expectedIndent;

        while (pos < this.tokens.length) {
            // Skip empty lines and comments
            if (this.isSkippableLine(pos)) {
                pos = this.skipToNextLine(pos);
                continue;
            }

            // Check indentation
            const indentToken = this.tokens[pos];
            if (indentToken.type === TokenType.Whitespace) {
                if (indentToken.value.length !== blockIndent) {
                    this.addError(
                        `Invalid indentation: expected ${blockIndent} spaces`,
                        indentToken
                    );
                }
                pos++;
            } else if (indentToken.type === TokenType.Keyword && !indentToken.indent) {
                // End of block when we find an unindented keyword
                return pos;
            } else if (!indentToken.indent) {
                this.addError('Invalid indentation', indentToken);
                return this.skipToNextLine(pos);
            }

            // Validate the line content
            pos = this.validateLine(pos);
        }

        return pos;
    }

    private validateLine(pos: number): number {
        const token = this.tokens[pos];

        // Handle boolean properties
        if (token.type === TokenType.Property) {
            return this.skipToNextLine(pos);
        }

        if (token.type === TokenType.Identifier || token.type === TokenType.String) {
            let current = pos + 1;

            // Skip whitespace after identifier
            while (current < this.tokens.length && 
                   this.tokens[current].type === TokenType.Whitespace) {
                current++;
            }

            if (current >= this.tokens.length) {
                this.addError('Unexpected end of file', token);
                return this.tokens.length;
            }

            const delimiter = this.tokens[current];
            if (delimiter.type === TokenType.Colon || delimiter.type === TokenType.Equals) {
                current++;

                // Skip whitespace after delimiter
                while (current < this.tokens.length && 
                       this.tokens[current].type === TokenType.Whitespace) {
                    current++;
                }

                // Validate property value
                if (current >= this.tokens.length || 
                    this.tokens[current].type === TokenType.LineBreak) {
                    this.addError('Expected property value', delimiter);
                    return delimiter.type === TokenType.Equals ? 
                           this.validateExpression(current) : 
                           this.skipToNextLine(current);
                }

                // Handle multi-line expressions
                if (delimiter.type === TokenType.Equals) {
                    return this.validateExpression(current);
                }

                // Handle single line value
                return this.skipToNextLine(current);
            }
        }

        return this.skipToNextLine(pos);
    }

    private validateExpression(startPos: number): number {
        const oldIndent = this.expectedIndent;
        this.expectedIndent += this.config.indentSize;
        
        let pos = startPos;
        while (pos < this.tokens.length) {
            const token = this.tokens[pos];

            if (this.isSkippableLine(pos)) {
                pos = this.skipToNextLine(pos);
                continue;
            }

            // Check expression indentation
            if (token.type === TokenType.Whitespace) {
                if (token.value.length !== this.expectedIndent) {
                    this.addError(
                        `Invalid expression indentation: expected ${this.expectedIndent} spaces`,
                        token
                    );
                }
                pos++;
                continue;
            }

            // End of expression when we find a token with less indentation
            if (token.indent * this.config.indentSize < oldIndent) {
                this.expectedIndent = oldIndent;
                return pos;
            }

            pos = this.skipToNextLine(pos);
        }

        this.expectedIndent = oldIndent;
        return pos;
    }

    private isSkippableLine(pos: number): boolean {
        const token = this.tokens[pos];
        return token.type === TokenType.LineBreak || 
               token.type === TokenType.Comment ||
               (token.type === TokenType.Whitespace && 
                this.peekNextType(pos) === TokenType.LineBreak);
    }

    private peekNextType(pos: number): TokenType | null {
        return pos + 1 < this.tokens.length ? this.tokens[pos + 1].type : null;
    }

    private skipToNextLine(pos: number): number {
        while (pos < this.tokens.length && 
               this.tokens[pos].type !== TokenType.LineBreak) {
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