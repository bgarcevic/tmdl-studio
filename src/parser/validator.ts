import { Token, TokenType, ParserDiagnostic, ParserConfig, DEFAULT_CONFIG } from './types';

export class TmdlValidator {
    private tokens: Token[];
    private diagnostics: ParserDiagnostic[] = [];
    private config: ParserConfig;

    constructor(tokens: Token[], config: ParserConfig = DEFAULT_CONFIG) {
        this.tokens = tokens;
        this.config = config;
    }

    /**
     * Validate the TMDL content based on tokens
     */
    public validate(): ParserDiagnostic[] {
        this.diagnostics = [];

        let pos = 0;
        while (pos < this.tokens.length) {
            // Skip whitespace and comments at top level
            while (pos < this.tokens.length && 
                   (this.tokens[pos].type === TokenType.Whitespace || 
                    this.tokens[pos].type === TokenType.LineBreak ||
                    this.tokens[pos].type === TokenType.Comment)) {
                pos++;
            }

            if (pos >= this.tokens.length) {
                break;
            }

            const token = this.tokens[pos];
            if (token.type !== TokenType.Keyword) {
                this.addError(
                    'Expected top-level declaration (database, model, table, relationship, or expression)',
                    token
                );
                pos = this.skipToNextLine(pos);
                continue;
            }

            pos = this.validateDeclaration(pos);
        }

        return this.diagnostics;
    }

    private validateDeclaration(pos: number): number {
        pos++; // Skip keyword

        // Skip whitespace after keyword
        while (pos < this.tokens.length && 
               this.tokens[pos].type === TokenType.Whitespace) {
            pos++;
        }

        // Validate name
        if (pos >= this.tokens.length || 
            (this.tokens[pos].type !== TokenType.Identifier && 
             this.tokens[pos].type !== TokenType.String)) {
            return this.skipToNextLine(pos);
        }

        pos++; // Skip name

        // Skip to end of declaration line
        while (pos < this.tokens.length && 
               this.tokens[pos].type !== TokenType.LineBreak) {
            pos++;
        }
        if (pos < this.tokens.length) {
            pos++; // Skip line break
        }

        return this.validateBlock(pos);
    }

    private validateBlock(pos: number): number {
        const expectedIndent = this.config.indentSize;

        while (pos < this.tokens.length) {
            // Skip empty lines
            if (this.isEmptyLine(pos)) {
                pos = this.skipToNextLine(pos);
                continue;
            }

            // Check indentation level
            if (this.tokens[pos].type === TokenType.Whitespace) {
                if (this.tokens[pos].value.length !== expectedIndent) {
                    // Only add error if indentation is wrong and this isn't the end of block
                    const nextNonWhitespace = this.findNextNonWhitespace(pos);
                    if (nextNonWhitespace && 
                        nextNonWhitespace.type !== TokenType.LineBreak &&
                        nextNonWhitespace.type !== TokenType.Comment) {
                        this.addError('Invalid indentation', this.tokens[pos]);
                    }
                }
                pos++;
                continue;
            }

            // Check if we're still in the block
            if (this.tokens[pos].type === TokenType.Keyword && 
                !this.tokens[pos].indent) {
                return pos;
            }

            // Handle property or child declaration
            pos = this.validateProperty(pos);
        }

        return pos;
    }

    private validateProperty(pos: number): number {
        const token = this.tokens[pos];

        // Handle boolean properties
        if (token.type === TokenType.Property) {
            return this.skipToNextLine(pos);
        }

        // Handle regular properties
        if (token.type === TokenType.Identifier || token.type === TokenType.String) {
            pos++;

            // Skip whitespace
            while (pos < this.tokens.length && 
                   this.tokens[pos].type === TokenType.Whitespace) {
                pos++;
            }

            if (pos < this.tokens.length) {
                const delimiter = this.tokens[pos];
                if (delimiter.type === TokenType.Colon || 
                    delimiter.type === TokenType.Equals) {
                    pos++;

                    // Skip whitespace after delimiter
                    while (pos < this.tokens.length && 
                           this.tokens[pos].type === TokenType.Whitespace) {
                        pos++;
                    }

                    // Check for missing value
                    if (pos >= this.tokens.length || 
                        this.tokens[pos].type === TokenType.LineBreak) {
                        this.addError('Expected property value', delimiter);
                        return this.skipToNextLine(pos);
                    }

                    // For expressions, skip the rest of the block
                    if (delimiter.type === TokenType.Equals) {
                        return this.skipExpressionBlock(pos);
                    }
                }
            }
        }

        return this.skipToNextLine(pos);
    }

    private skipExpressionBlock(pos: number): number {
        const startIndent = this.getLineIndent(pos);
        
        while (pos < this.tokens.length) {
            pos = this.skipToNextLine(pos);
            
            const lineIndent = this.getLineIndent(pos);
            if (lineIndent <= startIndent && !this.isEmptyLine(pos)) {
                break;
            }
        }

        return pos;
    }

    private getLineIndent(pos: number): number {
        if (pos >= this.tokens.length ||
            this.tokens[pos].type !== TokenType.Whitespace) {
            return 0;
        }
        return this.tokens[pos].value.length;
    }

    private isEmptyLine(pos: number): boolean {
        let current = pos;
        while (current < this.tokens.length && 
               this.tokens[current].type !== TokenType.LineBreak) {
            if (this.tokens[current].type !== TokenType.Whitespace && 
                this.tokens[current].type !== TokenType.Comment) {
                return false;
            }
            current++;
        }
        return true;
    }

    private findNextNonWhitespace(pos: number): Token | null {
        let current = pos + 1;
        while (current < this.tokens.length && 
               this.tokens[current].type === TokenType.Whitespace) {
            current++;
        }
        return current < this.tokens.length ? this.tokens[current] : null;
    }

    private skipToNextLine(pos: number): number {
        while (pos < this.tokens.length && 
               this.tokens[pos].type !== TokenType.LineBreak) {
            pos++;
        }
        return Math.min(pos + 1, this.tokens.length);
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