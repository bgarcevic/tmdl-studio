# TMDL Studio

[![Status](https://img.shields.io/badge/status-active%20development-blue)](https://github.com/bgarcevic/tmdl-studio)
[![VS Code](https://img.shields.io/badge/VS%20Code-%5E1.99.0-007ACC)](https://code.visualstudio.com/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

Explore, validate, and deploy TMDL semantic models directly from VS Code.

TMDL Studio combines:
- A VS Code extension (UI + commands)
- A standalone `timdle` CLI (core logic, CI/CD-ready)

## Features

- **TMDL Explorer** sidebar for browsing model objects
- **Validate Model** command for fast checks during editing
- **Deploy to Workspace** command for Fabric/Power BI deployment
- **Multiple auth modes**: interactive, service principal, environment variables
- **Auto-refresh** when supported model files are saved

## Quick Start

1. Open a folder containing a TMDL model.
2. Run `TMDL Studio: Select TMDL Model`.
3. Open **TMDL Explorer** in the Activity Bar.
4. Run `TMDL Studio: Validate Model`.
5. Run `TMDL Studio: Deploy to Workspace` when ready.

Project root detection supports:
- `definition/` folder with TMDL content
- `definition.pbism`
- `.platform`
- `.tmdl` files

## Screenshots

> Add screenshots/gifs in an `images/` folder and update these paths.

![TMDL Explorer](images/tmdl-explorer.png)
![Deploy Flow](images/deploy-flow.png)

## Install and Run From Source

Prerequisites:
- Node.js 20+
- npm
- .NET 8 SDK
- VS Code `^1.99.0`
- Recommended extension: `analysis-services.TMDL`

Build and run:

```bash
npm install
dotnet build timdle-core/Timdle.csproj
npm run compile
```

Then open the repo in VS Code and press `F5` to start the Extension Development Host.

## CLI Usage (Standalone)

`timdle` works without VS Code and is suitable for CI/CD pipelines.

```bash
timdle validate /path/to/model
timdle get-model-structure /path/to/model
timdle list-tables /path/to/model
timdle login --interactive
timdle deploy /path/to/model --interactive --workspace "https://api.fabric.microsoft.com/v1/workspaces/<id>"
```

## Authentication (CI/CD)

Environment variables:

```bash
TMDL_WORKSPACE_URL
TMDL_CLIENT_ID
TMDL_CLIENT_SECRET
TMDL_TENANT_ID
```

Example:

```bash
export TMDL_WORKSPACE_URL="https://api.fabric.microsoft.com/v1/workspaces/<id>"
export TMDL_CLIENT_ID="<client-id>"
export TMDL_CLIENT_SECRET="<client-secret>"
export TMDL_TENANT_ID="<tenant-id>"

timdle deploy /path/to/model --service-principal
```

## Developer Commands

```bash
npm run compile
npm run watch
npm run lint
npm run test

dotnet build timdle-core/Timdle.csproj
dotnet publish timdle-core/Timdle.csproj
dotnet test timdle-core.Tests/timdle-core.Tests.csproj
```

## Roadmap

- richer dependency visualization
- deeper semantic validation checks
- improved deployment preview and impact reporting
