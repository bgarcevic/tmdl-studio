import * as fs from 'fs';
import * as path from 'path';

/**
 * Utility for detecting the TMDL project root from a given path.
 */
export class ProjectRootDetector {
    private static readonly PROJECT_MARKERS = ['definition.pbism', '.platform', 'definition'];

    /**
     * Detects the TMDL project root by walking up the directory tree.
     * @param startPath - The path to start searching from (file or directory).
     * @returns The detected project root path, or null if not found.
     */
    static detectProjectRoot(startPath: string): string | null {
        let currentPath = path.resolve(startPath);

        while (true) {
            if (this.hasProjectMarker(currentPath)) {
                return currentPath;
            }

            const parentPath = path.dirname(currentPath);
            if (parentPath === currentPath) {
                break;
            }

            currentPath = parentPath;
        }

        return null;
    }

    /**
     * Checks if a directory contains any TMDL project markers.
     * @param dirPath - The directory path to check.
     * @returns True if the directory contains a project marker.
     */
    private static hasProjectMarker(dirPath: string): boolean {
        for (const marker of this.PROJECT_MARKERS) {
            const markerPath = path.join(dirPath, marker);
            if (fs.existsSync(markerPath)) {
                return true;
            }
        }
        return false;
    }
}
