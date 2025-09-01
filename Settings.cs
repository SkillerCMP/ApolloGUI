// ============================================================================
//  ApolloGUI — Settings.cs
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
using System.Text.Json;

namespace ApolloGUI
{
    public sealed class AppSettings
    {
        public string? DatabasePath { get; set; }
        public string? ToolsPath { get; set; }
        public string? PatcherPath { get; set; }
        public bool ShowMetadata { get; set; } = false;
        public bool BackupEnabled { get; set; } = true;
        public string? BackupsRoot { get; set; }

        public static string SettingsPath
        {
            get
            {
                var root = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(root, "settings.json");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
