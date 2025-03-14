# TMDL Inspector - VS Code Extension

## Overview/Goals

TMDL Inspector is a VS Code extension designed to help developers understand and visualize the dependencies within their Tabular Model Definition Language (TMDL) projects. The goal is to improve productivity, facilitate collaboration, and enhance the maintainability of tabular models defined in TMDL.

## Key Features

* Scan a folder for TMDL files.
* Parse TMDL files and identify model objects.
* Analyze dependencies between TMDL objects (relationships, measure dependencies, etc.).
* Visualize the dependency graph in an interactive WebView.
* Display details of selected TMDL objects and their dependencies.
* Allow searching for specific TMDL objects.
* Provide basic impact analysis capabilities.
* Report syntax errors and inconsistencies in TMDL files.

## Requirements

If you have any requirements or dependencies, add a section describing those and how to install and configure them.

## Extension Settings

Include if your extension adds any VS Code settings through the `contributes.configuration` extension point.

For example:

This extension contributes the following settings:

* `myExtension.enable`: Enable/disable this extension.
* `myExtension.thing`: Set to `blah` to do something.

## Backlog

| ID        | Type       | Title                                             | Description                                                                                                                                                                                                                           | Priority | Status     | Effort | Dependencies | Notes/Acceptance Criteria                                                                                                                                                                                             |
| :-------- | :--------- | :------------------------------------------------ | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | :------- | :--------- | :----- | :----------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| TI-FEAT-001 | Feature    | Implement TMDL Project Folder Selection         | Allow users to select a folder containing their TMDL project using a VS Code command. Store the selected path in the extension settings.                                                                                             | High     | To Do      | Small  |              | A command "TMDL Inspector: Select Project Folder" should be available. The selected folder path should be accessible by the extension.                                                                    |
| TI-FEAT-002 | Feature    | Implement Basic TMDL File Scanning            | Scan the selected project folder (and potentially subfolders) for files with the `.tmdl` extension.                                                                                                                                  | High     | To Do      | Small  | TI-FEAT-001 | Identify all `.tmdl` files within the specified directory structure.                                                                                                                                          |
| TI-CORE-001 | Feature    | Develop Core TMDL File Parser                   | Create a parser that can read and understand the basic syntax of TMDL files, identifying objects like `database`, `model`, `table`, `column`, `measure`, `relationship`, etc., and their properties.                                   | High     | To Do      | Large  | TI-FEAT-002 | The parser should correctly identify object declarations, names, and key properties as defined in the TMDL documentation.                                                                         |
| TI-CORE-002 | Feature    | Implement Relationship Dependency Analysis      | Analyze parsed TMDL objects to identify dependencies based on `fromColumn` and `toColumn` properties in `relationship` objects.                                                                                                       | High     | To Do      | Medium | TI-CORE-001 | The system should correctly link tables based on defined relationships.                                                                                                                                  |
| TI-VIS-001  | Feature    | Create Basic Visual Dependency Explorer (WebView) | Set up a VS Code WebView panel to display a visual representation of the dependency graph. Use a basic graph visualization library (e.g., a simple force-directed layout initially).                                                     | High     | To Do      | Medium | TI-CORE-002 | The WebView should load, and the initial graph should display tables and their relationships as nodes and edges.                                                                                   |
| TI-UI-001   | Feature    | Add "Analyze Dependencies" Command             | Implement a VS Code command that triggers the TMDL file scanning, parsing, and dependency analysis process.                                                                                                                             | High     | To Do      | Small  | TI-FEAT-002, TI-CORE-002 | Executing this command should initiate the backend analysis.                                                                                                                                                |
| TI-UI-002   | Feature    | Add "Visualize Dependencies" Command           | Implement a VS Code command that displays the visual dependency explorer WebView panel.                                                                                                                                                 | High     | To Do      | Small  | TI-VIS-001, TI-UI-001 | Executing this command should open the WebView showing the dependency graph generated by the "Analyze Dependencies" command.                                                              |
| TI-CORE-003 | Feature    | Implement Measure Dependency Analysis           | Analyze DAX expressions within `measure` objects to identify dependencies on tables, columns, and other measures.                                                                                                                        | Medium   | To Do      | Medium | TI-CORE-001 | The system should identify and link measures to the tables and columns they reference in their DAX expressions.                                                                                    |
| TI-UI-003   | Feature    | Implement Node Selection in Visual Explorer      | Allow users to select nodes (TMDL objects) in the WebView.                                                                                                                                                                           | Medium   | To Do      | Small  | TI-VIS-001 | Clicking on a node in the graph should trigger an event that can be handled by the extension's backend to display details.                                                                             |
| TI-UI-004   | Feature    | Display Object Details View (Side Panel)        | When a node is selected in the visual explorer, display detailed information about the object (e.g., properties) and its immediate dependencies in a VS Code side panel.                                                              | Medium   | To Do      | Medium | TI-UI-003, TI-CORE-002 | A dedicated side panel should appear with the name and key properties of the selected object, along with lists of its incoming and outgoing dependencies.                                                |
| TI-FEAT-003 | Feature    | Implement Dependency Search                     | Provide an input field in the WebView or a separate view for users to search for TMDL objects by name. Highlight the found objects in the visual explorer.                                                                             | Medium   | To Do      | Medium | TI-VIS-001, TI-CORE-001 | Users should be able to type in a name and see matching nodes highlighted in the graph.                                                                                                 |
| TI-CORE-004 | Feature    | Implement Column Dependency Analysis            | Analyze properties like `sourceColumn` and `sortByColumn` in `column` objects and column references within hierarchies to identify dependencies.                                                                                         | Medium   | To Do      | Medium | TI-CORE-001 | The system should correctly link columns to their source columns and identify columns used for sorting or in hierarchies.                                                                       |
| TI-IMPR-001 | Improvement| Improve Visual Graph Layout                     | Explore different layout options in the graph visualization library to make the dependency graph more readable and understandable (e.g., hierarchical layout for certain dependency types).                                                | Low      | To Do      | Small  | TI-VIS-001 | The default graph layout should be reasonably clear. Provide options for different layouts if possible.                                                                                              |
| TI-BUG-001  | Bug        | Handle Parsing Errors Gracefully                | Ensure that the extension doesn't crash or throw unhandled exceptions when encountering syntax errors or unexpected structures in TMDL files. Report these errors to the user in a clear way (e.g., in the VS Code Problems panel). | High     | To Do      | Small  | TI-CORE-001 | The extension should provide informative error messages when parsing fails, ideally indicating the file and line number.                                                                            |
| TI-FEAT-004 | Feature    | Basic Impact Analysis (Direct Dependencies)     | When an object is selected, provide an option (e.g., context menu) to highlight its immediate upstream and downstream dependencies in the visual explorer.                                                                                | Low      | To Do      | Medium | TI-UI-003, TI-CORE-002 | Right-clicking on a node should offer options like "Show Upstream Dependencies" and "Show Downstream Dependencies," which visually emphasize the related nodes and edges.                                |
| TI-CORE-005 | Feature    | Implement Partition Dependency Analysis         | Analyze M and DAX expressions within `partition` objects to find references to tables and expressions.                                                                                                                                 | Low      | To Do      | Medium | TI-CORE-001 | The system should identify and link partitions to the tables and expressions they reference.                                                                                                        |
| TI-CORE-006 | Feature    | Implement Role Dependency Analysis              | Analyze table and column permissions defined within `role` objects to identify dependencies.                                                                                                                                            | Low      | To Do      | Medium | TI-CORE-001 | The system should identify and link roles to the tables and columns they grant or deny access to.                                                                                                     |

## Future Considerations/Nice-to-Haves

* More advanced impact analysis (e.g., multi-level dependencies).
* Filtering the dependency graph by object type and dependency type.
* Customizable graph styling and appearance.
* Exporting the dependency graph as an image or other format.
* Integration with version control systems to show changes in dependencies over time.

## Out of Scope

* Editing or modifying TMDL files directly within the visualization.
* Support for older versions of Tabular Models (below compatibility level 1200).
* Full compatibility with the entire Tabular Object Model (TOM) API beyond dependency analysis.

## Known Issues

Calling out known issues can help limit users opening duplicate issues against your extension.

## Release Notes

Users appreciate release notes as you update your extension.

---

## Following extension guidelines

Ensure that you've read through the extensions guidelines and follow the best practices for creating your extension.

* [Extension Guidelines](https://code.visualstudio.com/api/references/extension-guidelines)

