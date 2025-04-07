import { Token, TokenType, Keywords, BooleanProperties, ParserConfig, DEFAULT_CONFIG } from './types';

export class TmdlTokenizer {
    private source: string;
    private pos: number = 0;
    private line: number = 1;
    private column: number = 1;
    private tokens: Token[] = [];
    private config: ParserConfig;

    constructor(source: string, config: ParserConfig = DEFAULT_CONFIG) {
        this.source = source;
        this.config = config;
    }

    /**
     * Tokenize the entire source text
     */
    public tokenize(): Token[] {
        this.tokens = [];
        this.pos = 0;
        this.line = 1;
        this.column = 1;

        while (!this.isEOF()) {
            // Skip whitespace at the start of a line (for indentation)
            if (this.column === 1) {
                const indent = this.tokenizeIndentation();
                if (indent > 0) {
                    continue;
                }
            }

            const char = this.peek();

            if (this.isWhitespace(char)) {
                // Skip non-indentation whitespace
                this.advance();
                this.column++;
            } else if (char === '\n' || char === '\r') {
                if (char === '\r' && this.peekNext() === '\n') {
                    this.advance(); // Skip \r in \r\n
                }
                this.addToken(TokenType.LineBreak, char === '\r' ? '\r\n' : '\n', 1);
                this.advance();
                this.line++;
                this.column = 1;
            } else if (char === '/') {
                this.tokenizeComment();
            } else if (char === ':') {
                this.addToken(TokenType.Colon, ':', 1);
                this.advance();
            } else if (char === '=') {
                this.addToken(TokenType.Equals, '=', 1);
                this.advance();
            } else if (char === '"' || char === "'") {
                this.tokenizeString();
            } else if (this.isIdentifierStart(char)) {
                this.tokenizeIdentifier();
            } else {
                this.addToken(TokenType.Invalid, char, 1);
                this.advance();
            }
        }

        return this.tokens;
    }

    private tokenizeIndentation(): number {
        let spaces = 0;
        let start = this.pos;

        while (!this.isEOF() && this.isWhitespace(this.peek())) {
            spaces++;
            this.advance();
        }

        if (spaces > 0) {
            const indent = Math.floor(spaces / this.config.indentSize);
            this.addToken(TokenType.Whitespace, this.source.substring(start, this.pos), spaces);
            return spaces;
        }

        return 0;
    }

    private tokenizeComment(): void {
        if (this.match('///')) {
            let start = this.pos;
            let length = 3;
            this.pos += 3;
            this.column += 3;

            while (!this.isEOF() && this.peek() !== '\n' && this.peek() !== '\r') {
                length++;
                this.advance();
            }

            const value = this.source.substring(start, start + length);
            this.addToken(TokenType.Comment, value, length);
        } else {
            this.addToken(TokenType.Invalid, '/', 1);
            this.advance();
        }
    }

    private tokenizeString(): void {
        const quote = this.peek();
        let value = quote;
        let length = 1;
        this.advance();

        while (!this.isEOF() && this.peek() !== quote && this.peek() !== '\n' && this.peek() !== '\r') {
            if (this.peek() === '\\' && this.peekNext() === quote) {
                value += this.peek() + this.peekNext();
                length += 2;
                this.advance();
                this.advance();
            } else {
                value += this.peek();
                length++;
                this.advance();
            }
        }

        if (this.peek() === quote) {
            value += quote;
            length++;
            this.advance();
            this.addToken(TokenType.String, value, length);
        } else {
            this.addToken(TokenType.Invalid, value, length);
        }
    }

    private tokenizeIdentifier(): void {
        let start = this.pos;
        let length = 0;

        while (!this.isEOF() && this.isIdentifierPart(this.peek())) {
            length++;
            this.advance();
        }

        const value = this.source.substring(start, start + length);
        const type = this.getIdentifierType(value);
        this.addToken(type, value, length);
    }

    private getIdentifierType(value: string): TokenType {
        if (Keywords.includes(value.toLowerCase() as any)) {
            return TokenType.Keyword;
        } else if (BooleanProperties.includes(value.toLowerCase() as any)) {
            return TokenType.Property;
        }
        return TokenType.Identifier;
    }

    private addToken(type: TokenType, value: string, length: number): void {
        const indent = type === TokenType.Whitespace ? Math.floor(value.length / this.config.indentSize) : 0;
        this.tokens.push({
            type,
            value,
            line: this.line,
            column: this.column,
            length,
            indent
        });
        this.column += length;
    }

    private advance(): void {
        this.pos++;
    }

    private peek(): string {
        return this.isEOF() ? '\0' : this.source[this.pos];
    }

    private peekNext(): string {
        return this.pos + 1 >= this.source.length ? '\0' : this.source[this.pos + 1];
    }

    private match(text: string): boolean {
        if (this.pos + text.length > this.source.length) {
            return false;
        }
        return this.source.substring(this.pos, this.pos + text.length) === text;
    }

    private isEOF(): boolean {
        return this.pos >= this.source.length;
    }

    private isWhitespace(char: string): boolean {
        return char === ' ' || char === '\t';
    }

    private isIdentifierStart(char: string): boolean {
        return /[a-zA-Z_]/.test(char);
    }

    private isIdentifierPart(char: string): boolean {
        return /[a-zA-Z0-9_]/.test(char);
    }
}