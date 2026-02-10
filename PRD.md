# PRD: TMDL Studio (v1.0)

**Author:** Boris Kamber-Garcevic

**Status:** In Development

**Target Platform:** VS Code (Cross-platform: Windows & macOS)

**Backend:** .NET 8.0 (timdle-core)

---

## 1. Executive Summary

**TMDL Studio** is a developer-centric VS Code extension designed to provide a "Tabular Editor" style experience for Mac and Windows. It bridges the gap between raw TMDL files and the Microsoft Fabric/Power BI service, offering semantic visualization, dependency analysis, and professional deployment workflows.

---

## 2. Strategic Objectives

* **Pro-Dev Productivity:** Reduce cognitive load when navigating complex DAX lineages.
* **Platform Freedom:** Provide 100% feature parity for Mac-based BI developers.
* **Operational Excellence:** Move away from "Desktop-first" deployments to a "Code-first" (Git-compatible) lifecycle.

---

## 3. Core Features (The "Excellent" Few)

### 3.1 Interactive Tabular Explorer (The Sidebar)

* **Description:** A high-density TreeView of the Tabular Object Model.
* **Requirements:**
* Grouping by Table, Measure, and Partition.
* Real-time search/filtering of model objects.
* One-click navigation to the corresponding `.tmdl` source file.


* **KPI:** Successful load of models with >500 objects in <200ms.

### 3.2 DAX Dependency Graph (Visual Lineage)

* **Description:** A visual "map" showing how measures and columns relate.
* **Requirements:**
* Webview-based node-link diagram.
* Highlight "Precedents" (What does this measure use?) and "Dependents" (What uses this measure?).
* Visual indicators for broken references or circular dependencies.


* **KPI:** Ability to trace a measure 3+ levels deep without manually searching files.

### 3.3 Semantic Guardrails (Best Practice Analyzer)

* **Description:** An integrated "Linter" for Tabular Models.
* **Requirements:**
* Automated checks for: Missing descriptions, unused columns, and "naked" columns in visuals.
* VS Code "Problems" pane integration for validation errors.


* **KPI:** Reduction in "sloppy" metadata before the model hits Production.

### 3.4 Fabric/XMLA Deployment Engine

* **Description:** Direct deployment from VS Code to Microsoft Fabric/Power BI.
* **Requirements:**
* Support for **XMLA Read/Write** endpoints.
* **Secret Management:** Secure storage of Workspace URLs and Service Principals using VS Code SecretStorage.
* **Impact Analysis:** A "Diff" summary before deployment (e.g., "Add 2 measures, Update 1 table").


* **KPI:** 0% failure rate for metadata-only deployments to Fabric capacities.

---

## 4. Technical Architecture

### 4.1 Hybrid Architecture

* **Frontend:** TypeScript/VS Code API. Handles UI, State, and Secret management.
* **Backend:** `timdle-core` (C# .NET 8). Handles TOM interop, DAX parsing, and XMLA communication.
* **Communication:** JSON-based RPC via standard I/O (STDOUT/STDIN).

### 4.2 Deployment Strategy

* **Platform-Specific Bundling:** Use `vsce` to package native binaries for `win-x64` and `osx-arm64`.
* **Self-Contained:** The C# engine must include the .NET runtime to remove user-side dependencies.

---

## 5. User Workflow (The "Happy Path")

1. **Open:** User opens a folder containing a `.SemanticModel` or TMDL files.
2. **Explore:** User clicks a measure in the "Timdle Explorer" to see its lineage.
3. **Edit:** User modifies a DAX expression in the editor (Microsoft's TMDL extension provides syntax highlighting).
4. **Validate:** On save, `timdle-core` validates the logic and checks BPA rules.
5. **Deploy:** User clicks "Deploy to Fabric," selects the "Dev" environment, and the model is updated via XMLA.

---

## 6. Success Metrics

* **Feature Parity:** All core features work identically on Apple Silicon (M1/M2/M3) and Windows.
* **Performance:** CLI execution overhead stays under 100ms for metadata discovery.
* **Adoption:** Successful deployment to a Fabric workspace without using Power BI Desktop.

---

## 7. Roadmap

* **V1.1:** Fabric Deployment (XMLA) + Secret Management.
* **V1.2:** Visual Dependency Graph (Webview).
* **V1.3:** BPA (Best Practice Analyzer) rule engine.

### 7.1 CLI Enhancements (Timdle CLI Improvements)

* **V0.9 (CLI Foundation):**
  * Add shell completions (bash/zsh/fish) for tab-completion support
  * Create install script to add `timdle` to user PATH
  * Publish as self-contained single-file binary for easy distribution
  * Add `deploy` command for model deployment to Fabric/Power BI
