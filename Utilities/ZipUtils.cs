// ============================================================================
//  ApolloGUI — ZipUtils.cs
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

namespace ApolloGUI.Utilities
{
    /// <summary>
    /// Framework-neutral zip extraction helpers.
    /// </summary>
    public static class ZipUtils
    {
        /// <summary>
        /// Streams a single ZipArchiveEntry out to a file.
        /// </summary>
        public static void ExtractToFile(ZipArchiveEntry entry, string destinationPath, bool overwrite)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (destinationPath == null) throw new ArgumentNullException(nameof(destinationPath));

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var source = entry.Open();
            using var target = new FileStream(destinationPath, overwrite ? FileMode.Create : FileMode.CreateNew,
                                              FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }

        /// <summary>
        /// Extract all entries to a directory.
        /// </summary>
        public static void ExtractAll(ZipArchive archive, string destinationDirectory, bool overwrite = true)
        {
            if (archive == null) throw new ArgumentNullException(nameof(archive));
            Directory.CreateDirectory(destinationDirectory);

            foreach (var e in archive.Entries)
            {
                var fullPath = Path.Combine(destinationDirectory, e.FullName);
                if (string.IsNullOrEmpty(e.Name))
                {
                    Directory.CreateDirectory(fullPath);
                    continue;
                }
                ExtractToFile(e, fullPath, overwrite);
            }
        }
    }
}
