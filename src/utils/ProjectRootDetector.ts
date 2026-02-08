import * as fs from 'fs';
import * as path from 'path';

/**
 * Utility for detecting the TMDL project root from a given path.
 */
export class ProjectRootDetector {
    /**
     * Detects the TMDL project root by walking up the directory tree.
     * Prioritizes finding the 'definition' folder structure.
     * @param startPath - The path to start searching from (file or directory).
     * @returns The detected project root path, or null if not found.
     */
    static detectProjectRoot(startPath: string): string | null {
        let currentPath = path.resolve(startPath);

        // If startPath is a file, start from its directory
        if (fs.existsSync(currentPath) && fs.statSync(currentPath).isFile()) {
            currentPath = path.dirname(currentPath);
        }

        // PHASE 1: Walk up and look for a 'definition' folder first
        // This takes priority over everything else
        let checkPath = currentPath;
        while (true) {
            const definitionPath = path.join(checkPath, 'definition');
            if (fs.existsSync(definitionPath) && fs.statSync(definitionPath).isDirectory()) {
                // Found a definition folder - this is our project root
                // But verify it actually contains TMDL content
                if (this.hasTmdlContent(definitionPath)) {
                    return checkPath;
                }
            }

            const parentPath = path.dirname(checkPath);
            if (parentPath === checkPath) {
                break;
            }
            checkPath = parentPath;
        }

        // PHASE 2: No definition folder found, fall back to legacy detection
        // Check for marker files and .tmdl files in current directory
        checkPath = currentPath;
        while (true) {
            // Check for marker files
            const fileMarkers = ['definition.pbism', '.platform'];
            for (const marker of fileMarkers) {
                const markerPath = path.join(checkPath, marker);
                if (fs.existsSync(markerPath) && fs.statSync(markerPath).isFile()) {
                    return checkPath;
                }
            }

            // Check for .tmdl files directly (legacy structure without definition folder)
            try {
                const files = fs.readdirSync(checkPath);
                const hasTmdlFiles = files.some(file => file.endsWith('.tmdl'));
                if (hasTmdlFiles) {
                    return checkPath;
                }
            } catch (error) {
                // Continue to parent
            }

            const parentPath = path.dirname(checkPath);
            if (parentPath === checkPath) {
                break;
            }
            checkPath = parentPath;
        }

        return null;
    }

    /**
     * Checks if a directory contains TMDL content (files or subdirectories).
     */
    private static hasTmdlContent(dirPath: string): boolean {
        try {
            const files = fs.readdirSync(dirPath);
            
            // Check for .tmdl files
            const hasTmdlFiles = files.some(file => file.endsWith('.tmdl'));
            if (hasTmdlFiles) {
                return true;
            }

            // Check for TMDL subdirectories
            const tmdlSubdirs = ['tables', 'cultures', 'relationships'];
            const hasTmdlSubdirs = tmdlSubdirs.some(subdir => {
                const subdirPath = path.join(dirPath, subdir);
                return fs.existsSync(subdirPath) && fs.statSync(subdirPath).isDirectory();
            });
            if (hasTmdlSubdirs) {
                return true;
            }

            return false;
        } catch (error) {
            return false;
        }
    }
}
