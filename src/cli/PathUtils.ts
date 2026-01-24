import * as vscode from 'vscode';
import * as path from 'path';

/**
 * Utility class for platform-specific CLI path resolution.
 */
export class PathUtils {
    /**
     * Gets the absolute path to the TMDL CLI executable.
     * @param context - The VS Code extension context.
     * @returns The absolute path to the CLI executable.
     */
    static getCliPath(context: vscode.ExtensionContext): string {
        const executableName = PathUtils.getExecutableName();
        const rid = PathUtils.getRuntimeId();

        return context.asAbsolutePath(path.join(
            'timdle-core', 'bin', 'Debug', 'net8.0', rid, executableName
        ));
    }

    /**
     * Gets the platform-specific executable name.
     * @returns 'timdle.exe' on Windows, 'timdle' otherwise.
     */
    static getExecutableName(): string {
        const isWindows = process.platform === 'win32';
        return isWindows ? 'timdle.exe' : 'timdle';
    }

    /**
     * Gets the runtime identifier for the current platform.
     * @returns 'win-x64' on Windows, 'osx-arm64' on macOS ARM.
     */
    static getRuntimeId(): string {
        const isWindows = process.platform === 'win32';
        return isWindows ? 'win-x64' : 'osx-arm64';
    }
}
