// ============================================================================
//  ApolloGUI — RestoreWindow.xaml.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ApolloGUI
{
    public partial class RestoreWindow : System.Windows.Window
    {
        public sealed class Item
        {
            public string File { get; init; } = "";
            public DateTime Timestamp { get; init; }
            public string Path { get; init; } = "";
        }

        readonly string gameFolder;
        readonly string sanitizedModName;
        readonly string targetPath;

        public string? SelectedZipPath { get; private set; }

        public RestoreWindow(string gameFolder, string modName, string targetPath)
        {
            InitializeComponent();
            this.gameFolder = gameFolder;
            this.sanitizedModName = BackupManager.Sanitize(modName);
            this.targetPath = targetPath;

            txtInfo.Text = $"Backups for: {modName}";
            LoadBackups();
        }

        void LoadBackups()
        {
            lstBackups.Items.Clear();
            if (!Directory.Exists(gameFolder)) return;

            var prefix = $"({sanitizedModName})_";
            var zips = Directory.EnumerateFiles(gameFolder, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(p => System.IO.Path.GetFileName(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            var items = new List<Item>();
            foreach (var z in zips)
            {
                var name = System.IO.Path.GetFileName(z);
                var tsPart = name.Substring(prefix.Length).Replace(".zip", "");
                DateTime ts;
                if (!DateTime.TryParseExact(tsPart, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out ts))
                    ts = System.IO.File.GetCreationTime(z);

                items.Add(new Item { File = name, Timestamp = ts, Path = z });
            }
            foreach (var it in items.OrderByDescending(i => i.Timestamp))
                lstBackups.Items.Add(it);
        }

        void Restore_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (lstBackups.SelectedItem is not Item it) { System.Windows.MessageBox.Show(this, "Select a backup.zip", "Restore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information); return; }
            SelectedZipPath = it.Path;
            DialogResult = true;
            Close();
        }

        void OpenFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var psi = new ProcessStartInfo
            {
                FileName = gameFolder,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
    }
}
