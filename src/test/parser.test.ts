import * as assert from 'assert';
import { TmdlTokenizer } from '../parser/tokenizer';
import { TmdlValidator } from '../parser/validator';
import { TokenType, DEFAULT_CONFIG } from '../parser/types';

suite('TMDL Parser Test Suite', () => {
    test('Tokenizer - Basic Tokens', () => {
        const source = `database MyDB
    compatibilityLevel: 1567`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();

        assert.strictEqual(tokens.length, 7);
        assert.strictEqual(tokens[0].type, TokenType.Keyword);
        assert.strictEqual(tokens[0].value, 'database');
        assert.strictEqual(tokens[1].type, TokenType.Identifier);
        assert.strictEqual(tokens[1].value, 'MyDB');
    });

    test('Tokenizer - String Literals', () => {
        const source = `table 'My Table'
    column "String Col"`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();

        const stringTokens = tokens.filter(t => t.type === TokenType.String);
        assert.strictEqual(stringTokens.length, 2);
        assert.strictEqual(stringTokens[0].value, "'My Table'");
        assert.strictEqual(stringTokens[1].value, '"String Col"');
    });

    test('Tokenizer - Comments', () => {
        const source = `/// This is a description
table MyTable`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();

        assert.strictEqual(tokens[0].type, TokenType.Comment);
        assert.strictEqual(tokens[0].value, '/// This is a description');
    });

    test('Validator - Valid Database', () => {
        const source = `database MyDB
    compatibilityLevel: 1567
    
    model Model
        culture: en-US`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();
        
        const validator = new TmdlValidator(tokens, DEFAULT_CONFIG);
        const diagnostics = validator.validate();

        assert.strictEqual(diagnostics.length, 0);
    });

    test('Validator - Invalid Indentation', () => {
        const source = `database MyDB
  compatibilityLevel: 1567
      model Model
    culture: en-US`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();
        
        const validator = new TmdlValidator(tokens, DEFAULT_CONFIG);
        const diagnostics = validator.validate();

        assert.ok(diagnostics.length > 0);
        assert.ok(diagnostics.some(d => d.message.includes('indentation')));
    });

    test('Validator - Missing Property Value', () => {
        const source = `database MyDB
    compatibilityLevel:
    model Model`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();
        
        const validator = new TmdlValidator(tokens, DEFAULT_CONFIG);
        const diagnostics = validator.validate();

        assert.ok(diagnostics.length > 0);
        assert.ok(diagnostics.some(d => d.message.includes('property value')));
    });

    test('Validator - Expression Property', () => {
        const source = `table Sales
    measure 'Total Sales' = 
        SUM(Sales[Amount])
        formatString: Currency`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();
        
        const validator = new TmdlValidator(tokens, DEFAULT_CONFIG);
        const diagnostics = validator.validate();

        assert.strictEqual(diagnostics.length, 0);
    });

    test('Validator - Boolean Property', () => {
        const source = `table Sales
    column ID
        isKey
        isHidden`;

        const tokenizer = new TmdlTokenizer(source, DEFAULT_CONFIG);
        const tokens = tokenizer.tokenize();
        
        const validator = new TmdlValidator(tokens, DEFAULT_CONFIG);
        const diagnostics = validator.validate();

        assert.strictEqual(diagnostics.length, 0);
    });
});