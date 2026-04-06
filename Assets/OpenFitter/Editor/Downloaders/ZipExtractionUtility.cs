#nullable enable
using System;
using System.IO;
using System.IO.Compression;

namespace OpenFitter.Editor.Downloaders
{
    /// <summary>
    /// Utility for extracting zip files with common folder handling.
    /// </summary>
    public static class ZipExtractionUtility
    {
        /// <summary>
        /// Extracts a zip file to a final directory, handling wrapped folder structures.
        /// GitHub zips typically contain a single wrapper folder.
        /// </summary>
        /// <param name="zipPath">Path to the zip file to extract</param>
        /// <param name="finalTargetPath">Final destination directory path</param>
        public static void ExtractZipToDirectory(string zipPath, string finalTargetPath)
        {
            if (string.IsNullOrEmpty(zipPath)) throw new ArgumentNullException(nameof(zipPath));
            if (string.IsNullOrEmpty(finalTargetPath)) throw new ArgumentNullException(nameof(finalTargetPath));

            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("Zip file not found: " + zipPath);
            }

            string tempExtractPath = Path.Combine(Path.GetTempPath(), "OpenFitter_" + Path.GetRandomFileName());

            try
            {
                // Clean up old temp if exists
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }

                // Clean up final target if exists
                if (Directory.Exists(finalTargetPath))
                {
                    Directory.Delete(finalTargetPath, true);
                }

                Directory.CreateDirectory(tempExtractPath);

                // Extract ZIP to temp location
                ZipFile.ExtractToDirectory(zipPath, tempExtractPath);

                // Find the inner folder (GitHub zips have a wrapper folder like "repo-main")
                string[] extractedDirs = Directory.GetDirectories(tempExtractPath);

                // Strict check: GitHub zips usually have exactly one wrapper. 
                // If multiple or zero, we fallback to tempExtractPath itself.
                string sourceDir = extractedDirs.Length == 1 ? extractedDirs[0] : tempExtractPath;

                // Ensure parent directory of target exists
                string? parentDir = Path.GetDirectoryName(finalTargetPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir!);
                }

                // Move extracted content to final location.
                // Directory.Move cannot cross volume boundaries on Windows, so
                // fall back to copy+delete in that case.
                if (AreOnSameRoot(sourceDir, finalTargetPath))
                {
                    Directory.Move(sourceDir, finalTargetPath);
                }
                else
                {
                    CopyDirectoryRecursively(sourceDir, finalTargetPath);
                    Directory.Delete(sourceDir, true);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[OpenFitter] Extraction failed: {ex.Message}");
                throw; // Rethrow to let caller handle
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempExtractPath))
                {
                    try
                    {
                        Directory.Delete(tempExtractPath, true);
                    }
                    catch (Exception cleanupEx)
                    {
                        UnityEngine.Debug.LogWarning($"[OpenFitter] Temp cleanup failed: {cleanupEx.Message}");
                    }
                }
            }
        }

        private static bool AreOnSameRoot(string sourcePath, string destinationPath)
        {
            string sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath)) ?? string.Empty;
            string destinationRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath)) ?? string.Empty;
            return string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void CopyDirectoryRecursively(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(filePath);
                string destinationFilePath = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destinationFilePath, overwrite: true);
            }

            foreach (string subDirPath in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDirPath);
                string destinationSubDirPath = Path.Combine(destinationDir, subDirName);
                CopyDirectoryRecursively(subDirPath, destinationSubDirPath);
            }
        }
    }
}
