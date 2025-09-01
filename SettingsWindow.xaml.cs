// ============================================================================
//  ApolloGUI — SettingsWindow.xaml.cs
//  Purpose: Settings dialog logic: theme, paths, filters.
//  Key types: SettingsWindow; binding properties.
//  Notes: Persist to JSON; update theme in real-time.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ApolloGUI
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings? _settings;

        // Baseline expects this constructor
        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            // Populate UI from settings
            try
            {
                txtDb.Text      = settings.DatabasePath ?? txtDb.Text;
                txtTools.Text   = settings.ToolsPath   ?? txtTools.Text;
                txtBackups.Text = settings.BackupsRoot ?? txtBackups.Text;
                chkMetadata.IsChecked = settings.ShowMetadata;
                chkBackup.IsChecked   = settings.BackupEnabled;
            }
            catch { /* ignore initial load issues */ }
        }

        // Keep a parameterless ctor too (designer support / optional)
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private static string? PickFolder(string? initialPath = null)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select Folder"
            };
            try
            {
                if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
                    dlg.InitialDirectory = initialPath;
            }
            catch { }
            var ok = dlg.ShowDialog();
            if (ok == true)
            {
                try
                {
                    var selected = Path.GetDirectoryName(dlg.FileName);
                    if (!string.IsNullOrEmpty(selected) && Directory.Exists(selected))
                        return selected;
                }
                catch { }
            }
            return null;
        }

        private void BrowseDb_Click(object sender, RoutedEventArgs e)
        {
            var chosen = PickFolder(txtDb?.Text);
            if (!string.IsNullOrEmpty(chosen)) txtDb.Text = chosen;
        }

        private void BrowseTools_Click(object sender, RoutedEventArgs e)
        {
            var chosen = PickFolder(txtTools?.Text);
            if (!string.IsNullOrEmpty(chosen)) txtTools.Text = chosen;
        }

        private void BrowseBackups_Click(object sender, RoutedEventArgs e)
        {
            var chosen = PickFolder(txtBackups?.Text);
            if (!string.IsNullOrEmpty(chosen)) txtBackups.Text = chosen;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // Push values back into settings if available
            if (_settings != null)
            {
                _settings.DatabasePath = txtDb.Text;
                _settings.ToolsPath    = txtTools.Text;
                _settings.BackupsRoot  = txtBackups.Text;
                _settings.ShowMetadata = chkMetadata.IsChecked == true;
                _settings.BackupEnabled= chkBackup.IsChecked == true;
                try { _settings.Save(); } catch { }
            }
            this.DialogResult = true;
            this.Close();
        }
    }
}
