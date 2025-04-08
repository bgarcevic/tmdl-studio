import * as assert from 'assert';
import { TmdlTokenizer } from '../parser/tokenizer';
import { TokenType } from '../parser/types';

suite('TMDL Tokenizer Test Suite', () => {
    test('Empty input returns empty token array', () => {
        const tokenizer = new TmdlTokenizer('');
        const tokens = tokenizer.tokenize();
        assert.strictEqual(tokens.length, 0);
    });

    test('Tokenizes simple identifier', () => {
        const tokenizer = new TmdlTokenizer('myIdentifier');
        const tokens = tokenizer.tokenize();
        
        assert.strictEqual(tokens.length, 1);
        assert.strictEqual(tokens[0].type, TokenType.Identifier);
        assert.strictEqual(tokens[0].value, 'myIdentifier');
    });

    test('Tokenizes keywords correctly', () => {
        const tokenizer = new TmdlTokenizer('model database table');
        const tokens = tokenizer.tokenize();
        
        assert.strictEqual(tokens.length, 3);
        tokens.forEach(token => {
            assert.strictEqual(token.type, TokenType.Keyword);
        });
    });

    test('Handles string literals', () => {
        const tokenizer = new TmdlTokenizer('"Hello World"');
        const tokens = tokenizer.tokenize();
        
        assert.strictEqual(tokens.length, 1);
        assert.strictEqual(tokens[0].type, TokenType.String);
        assert.strictEqual(tokens[0].value, '"Hello World"');
    });

    test('Tokenizes comments', () => {
        const tokenizer = new TmdlTokenizer('/// This is a comment');
        const tokens = tokenizer.tokenize();
        
        assert.strictEqual(tokens.length, 1);
        assert.strictEqual(tokens[0].type, TokenType.Comment);
        assert.strictEqual(tokens[0].value, '/// This is a comment');
    });

    test('Handles indentation', () => {
        const tokenizer = new TmdlTokenizer('  indented');
        const tokens = tokenizer.tokenize();
        
        assert.strictEqual(tokens.length, 2);
        assert.strictEqual(tokens[0].type, TokenType.Whitespace);
        assert.strictEqual(tokens[1].type, TokenType.Identifier);
    });

    test('Tracks line and column numbers', () => {
        const tokenizer = new TmdlTokenizer('first\nsecond');
        const tokens = tokenizer.tokenize();
        
        assert.strictEqual(tokens[0].line, 1);
        assert.strictEqual(tokens[0].column, 1);
        assert.strictEqual(tokens[2].line, 2);
        assert.strictEqual(tokens[2].column, 1);
    });
});