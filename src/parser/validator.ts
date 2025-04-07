import { Token, TokenType, ParserDiagnostic, ParserConfig, DEFAULT_CONFIG } from './types';

export class TmdlValidator {
    private tokens: Token[];
    private diagnostics: ParserDiagnostic[] = [];
    private config: ParserConfig;
    private pos: number = 0;

    constructor(tokens: Token[], config: ParserConfig = DEFAULT_CONFIG) {
        this.tokens = tokens;
        this.config = config;
    }

    /**
     * Validate the TMDL content based on tokens.
     */
    public validate(): ParserDiagnostic[] {
        this.diagnostics = [];
        this.pos = 0;

        while (this.pos < this.tokens.length) {
            this.skipSkippable();
            if (this.pos >= this.tokens.length) break;
            const token = this.tokens[this.pos];
            if (token.type === TokenType.Keyword) {
                this.validateDeclaration(0); // Top-level declarations have indent 0.
            } else {
                this.addError('Expected top-level declaration', token);
                this.skipToNextLine();
            }
        }
        return this.diagnostics;
    }

    private validateDeclaration(expectedIndent: number): void {
        const keywordToken = this.tokens[this.pos];
        this.pos++; // Consume keyword.
        this.skipWhitespace();

        if (this.pos >= this.tokens.length ||
            (this.tokens[this.pos].type !== TokenType.Identifier &&
             this.tokens[this.pos].type !== TokenType.String)) {
            this.addError('Expected identifier or string literal after keyword', keywordToken);
            this.skipToNextLine();
            return;
        }
        this.pos++; // Consume identifier.

        this.skipToNextLine();
        this.skipSkippable(); // Skip any blank lines after declaration
        
        // Check for block content
        if (this.pos < this.tokens.length) {
            const nextIndent = this.getCurrentLineIndent();
            const expectedNextIndent = expectedIndent + this.config.indentSize;
            if (nextIndent === expectedNextIndent) {
                this.validateBlock(expectedNextIndent);
            } else if (nextIndent > expectedIndent) {
                // Wrong indentation level
                this.addError(
                    `Invalid indentation: expected ${expectedNextIndent} spaces, got ${nextIndent}`,
                    this.tokens[this.pos]
                );
                this.skipToNextLine();
            }
        }
    }

    private validateBlock(expectedIndent: number): void {
        while (this.pos < this.tokens.length) {
            this.skipSkippable();
            if (this.pos >= this.tokens.length) break;

            const currentIndent = this.getCurrentLineIndent();
            if (currentIndent < expectedIndent) {
                // End of block.
                return;
            } else if (currentIndent > expectedIndent) {
                // Wrong indentation level
                this.addError(
                    `Invalid indentation: expected ${expectedIndent} spaces, got ${currentIndent}`,
                    this.tokens[this.pos]
                );
                this.skipToNextLine();
                continue;
            }

            // At proper indent: skip indentation tokens if any.
            this.skipWhitespace();
            if (this.pos >= this.tokens.length) { break; }
            
            const token = this.tokens[this.pos];
            if (token.type === TokenType.Keyword) {
                // Handle nested declarations with next indentation level
                this.validateDeclaration(expectedIndent);
            } else if (
                token.type === TokenType.Identifier ||
                token.type === TokenType.String ||
                token.type === TokenType.Property
            ) {
                this.validatePropertyLine(expectedIndent);
            } else {
                this.addError('Expected property, keyword, or identifier', token);
                this.skipToNextLine();
            }
        }
    }

    private validatePropertyLine(currentBlockIndent: number): void {
        const token = this.tokens[this.pos];
        if (token.type === TokenType.Property) {
            // Boolean property shortcut.
            this.pos++; // Consume property token.
            this.skipToNextLine();
            return;
        }
        if (token.type === TokenType.Identifier || token.type === TokenType.String) {
            this.pos++; // Consume property name.
            this.skipWhitespace();
            if (this.pos >= this.tokens.length) {
                this.addError('Unexpected end of file after property name', token);
                return;
            }
            const delimiter = this.tokens[this.pos];
            if (delimiter.type === TokenType.Colon || delimiter.type === TokenType.Equals) {
                this.pos++; // Consume delimiter.
                this.skipWhitespace();
                if (this.pos >= this.tokens.length || this.tokens[this.pos].type === TokenType.LineBreak) {
                    this.addError('Expected property value', delimiter);
                    this.skipToNextLine();
                    return;
                }
                if (delimiter.type === TokenType.Equals) {
                    // Multi-line expression: check if next line is indented at least one level deeper.
                    const nextIndent = this.getCurrentLineIndent();
                    if (nextIndent >= currentBlockIndent + this.config.indentSize) {
                        this.validateBlock(currentBlockIndent + this.config.indentSize);
                    } else {
                        // Single-line expression.
                        this.skipToNextLine();
                    }
                } else {
                    // Colon-delimited property: value on same line.
                    this.skipToNextLine();
                }
            } else {
                this.addError('Expected : or = after property name', token);
                this.skipToNextLine();
            }
        } else {
            this.addError('Expected property name or boolean property', token);
            this.skipToNextLine();
        }
    }

    // Utility functions.

    /**
     * Determines the indentation of the current line by finding the first non-whitespace token on the line.
     * If the line is empty or starts with a LineBreak, returns 0.
     * Uses the token's column property (assumed 1-indexed) minus one.
     */
    private getCurrentLineIndent(): number {
        let tempPos = this.pos;
        // If current token is a LineBreak, indent is 0.
        if (tempPos < this.tokens.length && this.tokens[tempPos].type === TokenType.LineBreak) {
            return 0;
        }
        // Skip over whitespace tokens on the current line.
        while (tempPos < this.tokens.length && this.tokens[tempPos].type === TokenType.Whitespace) {
            tempPos++;
        }
        if (tempPos < this.tokens.length) {
            return this.tokens[tempPos].column - 1;
        }
        return 0;
    }

    private skipSkippable(): void {
        while (
            this.pos < this.tokens.length &&
            (this.tokens[this.pos].type === TokenType.LineBreak ||
             this.tokens[this.pos].type === TokenType.Comment ||
             (this.tokens[this.pos].type === TokenType.Whitespace && this.isWhitespaceOnlyLine(this.pos)))
        ) {
            this.pos++;
        }
    }

    private isWhitespaceOnlyLine(pos: number): boolean {
        let current = pos;
        while (current < this.tokens.length && this.tokens[current].type === TokenType.Whitespace) {
            current++;
        }
        return current >= this.tokens.length || this.tokens[current].type === TokenType.LineBreak;
    }

    private skipWhitespace(): void {
        while (this.pos < this.tokens.length && this.tokens[this.pos].type === TokenType.Whitespace) {
            this.pos++;
        }
    }

    private skipToNextLine(): void {
        while (this.pos < this.tokens.length && this.tokens[this.pos].type !== TokenType.LineBreak) {
            this.pos++;
        }
        if (this.pos < this.tokens.length) {
            this.pos++; // Consume LineBreak.
        }
    }

    private addError(message: string, token: Token): void {
        if (!this.diagnostics.some(d => d.line === token.line && d.column === token.column && d.message === message)) {
            this.diagnostics.push({
                message,
                severity: 'error',
                line: token.line,
                column: token.column,
                length: token.length
            });
        }
    }
}