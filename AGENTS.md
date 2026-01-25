# TMDL Studio - Agent Guidelines

This document provides guidelines for agentic coding assistants working on the TMDL Studio codebase.

## Project Overview

TMDL Studio is a VS Code extension for visualizing dependencies between DAX measures in TMDL files. It consists of:

- **TypeScript extension** (`src/`) - VS Code UI and integration
- **C# CLI tool** (`timdle-core/`) - Backend processing for TMDL models

## Build/Test/Lint Commands

### TypeScript (VS Code Extension)

```bash
npm run compile          # Compile TypeScript to out/
npm run watch           # Watch mode compilation
npm run lint            # Run ESLint on src/
npm run test            # Run all tests
npm run pretest         # Compile + lint (runs before test)
```

### C# (CLI Tool)

```bash
dotnet build            # Build the CLI (timdle-core/)
dotnet publish          # Publish as self-contained executable
```

### Running a Single Test

Use Mocha's grep flag to run a specific test:

```bash
npm test -- -g "test name"
npm test -- -g "Sample test"
```

## Code Style Guidelines

### TypeScript

#### Imports

- Group imports at file top, separated by blank lines:
  1. Node modules (`import * as vscode from 'vscode'`)
  2. Local modules (`import { TabularTreeProvider } from './views/explorer/TabularTreeProvider'`)
- Use namespace imports for Node built-ins: `import * as fs from 'fs'`
- Follow the Service Pattern: CLI code in `cli/`, UI code in `views/`, commands in `commands/`

#### Formatting

- Always use semicolons (enforced by ESLint)
- Use curly braces for all control flow blocks (enforced)
- Use single quotes for strings
- 4-space indentation

#### Types

- Strict mode enabled in tsconfig.json
- Explicit typing preferred for function parameters
- Use discriminated unions for type-safe state (e.g., TreeNode type)
- Type assertions only when necessary: `const structure = JSON.parse(stdout) as ModelStructure`
- Interface definitions above classes with JSDoc comments

#### Naming Conventions

- Classes: PascalCase (`TabularTreeProvider`)
- Interfaces: PascalCase (`ModelStructure`)
- Functions/Methods: camelCase (`getTreeItem`, `refresh`)
- Private methods: camelCase with underscore prefix (optional)
- Variables/Constants: camelCase (`currentTmdlFolder`, `executableName`)
- Type aliases: PascalCase (`TreeNode`)
- Event emitters: `_onDidChangeTreeData` pattern

#### Error Handling

- Use try-catch for async operations
- Display user-facing errors via `vscode.window.showErrorMessage()`
- Log technical errors to console or output channels
- Reject promises with Error objects

#### Documentation

- JSDoc comments for all public methods and interfaces
- Format: `/** Description. */` for single line, `/**\n * Description.\n */` for multi-line
- @param and @returns tags for function signatures

#### VS Code Extension Patterns

- Use `TreeDataProvider` interface for tree views
- Store state in `ExtensionContext.globalState`
- Register commands in `activate()` function
- Clean up in `deactivate()` function
- Use `vscode.ThemeIcon` for tree icons
- Tree items: `type` field for contextValue, `command` for click handlers

#### Service Pattern Architecture

This codebase follows the Service Pattern to cleanly separate concerns:

- **`cli/`** - The Engine Room: All CLI interactions and external process management
  - `TimdleClient.ts` - Service class wrapping all CLI calls (validate, getModelStructure, etc.)
  - `PathUtils.ts` - Platform-specific CLI path resolution utilities

- **`views/`** - The UI: All VS Code UI components
  - `explorer/TabularTreeProvider.ts` - TreeDataProvider for sidebar view
  - `explorer/ModelTreeItem.ts` - Tree node types and TreeItem formatting utilities

- **`commands/`** - VS Code Commands: Command handlers registered with VS Code
  - Commands are registered in `extension.ts` via static `register()` methods
  - Use dependency injection for services (e.g., TimdleClient)

- **`extension.ts`** - The Switchboard: Minimal wiring code only
  - No business logic
  - Only registration of commands and tree providers
  - Entry point for extension activation/deactivation

**Key Principles:**

- Isolate "messy" parts (CLI calls, parsing) from UI parts
- UI components should use service classes, not call CLI directly
- Commands are self-contained with their own service dependencies
- Configuration centralized in `config.ts`

### C# (CLI Tool)

#### Service Pattern Architecture

This codebase follows the Service Pattern to cleanly separate concerns:

- **`Services/`** - The Engine: All TOM interop and data transformation
  - `TmdlService.cs` - Centralized TOM access, DTO conversion, business logic
  - All `TmdlSerializer.DeserializeDatabaseFromFolder` calls
  - LINQ transformations and data mapping

- **`Models/`** - Data Transfer Objects: Type-safe JSON contracts
  - `ModelStructure.cs` - DTOs for model structure serialization
  - `ValidationResult.cs` - Structured validation results

- **`Commands/`** - Command Handlers: Thin wrappers around services
  - Commands call `TmdlService` methods and output JSON
  - Error handling at command level
  - `CommandRouter.cs` - Central command routing logic

- **`Program.cs`** - The Switchboard: Minimal entry point
  - Parse args, call `CommandRouter.Route()`
  - No business logic

**Key Principles:**

- TOM interop centralized in `Services/TmdlService.cs`
- DTOs provide type-safe JSON serialization
- Commands are thin wrappers around service methods
- Easy to add GUI or CI/CD integration later

#### Code Organization

- Namespace per directory/folder (`namespace TmdlStudio.Services`, `namespace TmdlStudio.Models`)
- Service layer (`Services/`) handles all TOM interop
- DTO layer (`Models/`) for type-safe JSON serialization
- Static classes for command handlers in `Commands/` directory
- Entry point: `Program.cs` with `Main()` method

#### Naming Conventions

- Classes: PascalCase (`GetModelStructureCommand`)
- Methods: PascalCase (`Execute`)
- Variables: camelCase
- Services: PascalCase with 'Service' suffix (`TmdlService`)
- DTOs: PascalCase with 'Info' suffix (`TableInfo`, `ModelInfo`)
- Command classes: PascalCase with 'Command' suffix (`GetTablesCommand`)
- XML Documentation comments (`///`) for all public members

#### Error Handling

- Wrap in try-catch blocks
- Console output for errors (simplistic approach)
- Structured `ValidationResult` class for validation responses

#### Build Configuration

- .NET 8.0 target framework
- Self-contained single-file publishing
- Runtime identifier: `osx-arm64` (macOS), `win-x64` (Windows)
- Package references in `.csproj` file

## Testing

- Tests are Mocha-style in `src/test/` directory
- Compiled to `out/test/` before running
- Use `assert` module for assertions
- Use `vscode` mock for VS Code API tests

## Project Structure

```
src/
  cli/                      # The Engine Room
    TimdleClient.ts         # Wraps all cp.exec calls to C# binary
    PathUtils.ts            # Handles finding .exe vs binary on Mac/Win
  views/                    # The UI
    explorer/               # The Sidebar TreeView logic
      TabularTreeProvider.ts
      ModelTreeItem.ts
    editor/                 # (Future) Custom editors for DAX/TMDL
  commands/                 # VS Code Commands
    ValidateCommand.ts
  config.ts                 # Central place to read vscode.workspace.getConfiguration
  extension.ts              # The "Switchboard" (Wiring only)
  test/
    extension.test.ts       # Test suite

timdle-core/
  Commands/
    CommandRouter.cs         # Central routing logic
    GetModelStructureCommand.cs
    GetTablesCommand.cs      # Renamed from ListTablesCommand
    ValidateCommand.cs
  Services/
    TmdlService.cs           # All TOM interop and DTO conversion
  Models/
    ModelStructure.cs        # DTOs for JSON serialization
    ValidationResult.cs      # Structured validation results
  Program.cs                # CLI entry point (minimal)
```

## Architecture Notes

- TypeScript extension follows Service Pattern architecture
- CLI interactions centralized in `cli/TimdleClient.ts` service class
- UI components (`views/`) use dependency injection to access CLI services
- Commands (`commands/`) encapsulate VS Code command logic with service dependencies
- Extension entry point (`extension.ts`) acts as minimal switchboard
- State persistence via VS Code's global state
- C# CLI tool follows Service Pattern architecture
- TOM interop centralized in `Services/TmdlService.cs`
- DTOs in `Models/` provide type-safe JSON contracts
- Commands are thin wrappers around service methods
- Entry point (`Program.cs`) acts as minimal switchboard

## Success Metrics

- **Feature Parity:** All core features work identically on Apple Silicon (M1/M2/M3) and Windows.
- **Performance:** CLI execution overhead stays under 100ms for metadata discovery.
- **Adoption:** Successful deployment to a Fabric workspace without using Power BI Desktop.
