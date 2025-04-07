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
            while (pos < this.tokens.length && this.isSkippable(this.tokens[pos])) {
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

            pos = this.validateTopLevelDeclaration(pos);
        }

        return this.diagnostics;
    }

    private validateTopLevelDeclaration(startPos: number): number {
        const token = this.tokens[startPos];
        let pos = startPos + 1;

        // Skip whitespace after keyword
        while (pos < this.tokens.length && this.tokens[pos].type === TokenType.Whitespace) {
            pos++;
        }

        // Validate identifier after keyword
        if (pos >= this.tokens.length || 
            (this.tokens[pos].type !== TokenType.Identifier && 
             this.tokens[pos].type !== TokenType.String)) {
            this.addError('Expected identifier or string literal after keyword', token);
            return this.skipToNextLine(startPos);
        }

        // Skip to end of line
        pos = this.skipToNextLine(pos);

        // Validate the block of properties
        return this.validateBlock(pos, this.config.indentSize);
    }

    private validateBlock(startPos: number, indent: number): number {
        let pos = startPos;
        const oldIndent = this.expectedIndent;
        this.expectedIndent = indent;

        while (pos < this.tokens.length) {
            // Skip empty lines and comments
            while (pos < this.tokens.length && this.isSkippable(this.tokens[pos])) {
                pos++;
            }

            if (pos >= this.tokens.length) {
                break;
            }

            const token = this.tokens[pos];
            
            // Check if we're still in the block
            if (this.getEffectiveIndent(token) < indent) {
                this.expectedIndent = oldIndent;
                return pos;
            }

            // Validate line content
            if (token.type === TokenType.Whitespace && 
                token.value.length !== this.expectedIndent) {
                this.addError(
                    `Invalid indentation: expected ${this.expectedIndent} spaces`,
                    token
                );
            }

            pos = this.validateProperty(pos);
        }

        this.expectedIndent = oldIndent;
        return pos;
    }

    private validateProperty(pos: number): number {
        // Skip initial whitespace
        while (pos < this.tokens.length && this.tokens[pos].type === TokenType.Whitespace) {
            pos++;
        }

        if (pos >= this.tokens.length) {
            return pos;
        }

        const token = this.tokens[pos];

        // Handle boolean properties
        if (token.type === TokenType.Property) {
            return this.skipToNextLine(pos);
        }

        if (token.type === TokenType.Identifier || token.type === TokenType.String) {
            let current = pos + 1;

            // Skip whitespace after property name
            while (current < this.tokens.length && 
                   this.tokens[current].type === TokenType.Whitespace) {
                current++;
            }

            if (current >= this.tokens.length) {
                return current;
            }

            const delimiter = this.tokens[current];
            if (delimiter.type === TokenType.Colon || delimiter.type === TokenType.Equals) {
                current++;

                // Handle multi-line expressions
                if (delimiter.type === TokenType.Equals) {
                    return this.validateExpression(current);
                }

                // Validate property value exists
                while (current < this.tokens.length && 
                       this.tokens[current].type === TokenType.Whitespace) {
                    current++;
                }

                if (current >= this.tokens.length || 
                    this.tokens[current].type === TokenType.LineBreak) {
                    this.addError('Expected property value', delimiter);
                }
            }
        }

        return this.skipToNextLine(pos);
    }

    private validateExpression(startPos: number): number {
        const expressionIndent = this.expectedIndent + this.config.indentSize;
        let pos = this.skipToNextLine(startPos);

        while (pos < this.tokens.length) {
            // Skip empty lines
            while (pos < this.tokens.length && this.isSkippable(this.tokens[pos])) {
                pos++;
            }

            if (pos >= this.tokens.length) {
                break;
            }

            const token = this.tokens[pos];
            const currentIndent = this.getEffectiveIndent(token);

            if (currentIndent < this.expectedIndent) {
                return pos;
            }

            if (token.type === TokenType.Whitespace && 
                token.value.length !== expressionIndent) {
                this.addError(
                    `Invalid expression indentation: expected ${expressionIndent} spaces`,
                    token
                );
            }

            pos = this.skipToNextLine(pos);
        }

        return pos;
    }

    private isSkippable(token: Token): boolean {
        return token.type === TokenType.LineBreak || 
               token.type === TokenType.Comment ||
               (token.type === TokenType.Whitespace && 
                this.peekNextType(this.tokens.indexOf(token)) === TokenType.LineBreak);
    }

    private getEffectiveIndent(token: Token): number {
        return token.type === TokenType.Whitespace ? 
               token.value.length : 
               token.indent * this.config.indentSize;
    }

    private peekNextType(pos: number): TokenType | null {
        return pos + 1 < this.tokens.length ? this.tokens[pos + 1].type : null;
    }

    private skipToNextLine(pos: number): number {
        while (pos < this.tokens.length && this.tokens[pos].type !== TokenType.LineBreak) {
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