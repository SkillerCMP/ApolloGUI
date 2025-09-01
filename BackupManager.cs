// ============================================================================
//  ApolloGUI — BackupManager.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

using System;
using System.IO;
using System.IO.Compression;

namespace ApolloGUI
{
    public static class BackupManager
    {
        public static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        public static string GetBackupsRoot(AppSettings settings, string appRoot)
        {
            var path = settings.BackupsRoot;
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(appRoot, "Backups");
            return path;
        }

        public static string EnsureGameFolder(AppSettings settings, string appRoot, string? gameName)
        {
            var root = GetBackupsRoot(settings, appRoot);
            var folder = Path.Combine(root, Sanitize(gameName ?? "UnknownGame"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string CreateBackupZip(AppSettings settings, string appRoot, string? gameName, string dataFilePath)
        {
            var gameFolder = EnsureGameFolder(settings, appRoot, gameName);
            var modName = Path.GetFileNameWithoutExtension(dataFilePath) ?? "data";
            modName = Sanitize(modName);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var zipName = $"({modName})_{stamp}.zip";
            var zipPath = Path.Combine(gameFolder, zipName);

            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entryName = Path.GetFileName(dataFilePath);
                zip.CreateEntryFromFile(dataFilePath, entryName, CompressionLevel.Optimal);
            }
            return zipPath;
        }
    }
}
