// ============================================================================
//  ApolloGUI — MainWindow.xaml.cs
//  Purpose: Main shell, commands, logging panel, and toolbar/menu handlers.
//  Key types: MainWindow partial class; RoutedUICommand; event handlers.
//  Notes: Integrates reset-to-database flow; ensure log auto-scroll stays enabled.
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
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO.Compression;                 // for ZipFileExtensions (ExtractToFile)
using Microsoft.Win32;                       // for Microsoft.Win32.OpenFileDialog
using System.ComponentModel;
using System.Windows.Data;


namespace ApolloGUI
{
    public partial class MainWindow : System.Windows.Window
    {

            private static byte[] BuildBytesForValue(ulong value, int widthBytes)
            {
                if (widthBytes < 1) widthBytes = 1;
                if (widthBytes > 8) widthBytes = 8;
                var le = BitConverter.GetBytes(value); // runtime little-endian
                var slice = new byte[widthBytes];
                Array.Copy(le, 0, slice, 0, widthBytes);
                return slice; // NO reverse here
            }
        

// ----- LINT NOTES (2025-08-31) -----------------------------------------------
// Reset-to-Database + Zip extraction:
// - Using System.IO.Compression ZipArchiveEntry.ExtractToFile requires the
//   'using System.IO.Compression' + 'System.IO.Compression.FileSystem' (for classic .NET Framework).
//   For .NET 8, the current using is sufficient.
// - Ensure extraction destination is created and existing files are overwritable when intended.
// Log auto-scroll:
// - Prefer a single behavior (attached property) rather than manual ScrollToEnd loops.
// Command wiring:
// - Keep all command bindings centralized (see CommandsInit) to avoid duplicate gestures.
// Save Wizard code generation:
// - Run MOD expansion first, then {Amount:...} prompts, then apply formatting/blocking
//   only for the final preview/export.
// ------------------------------------------------------------------------------

        class DbEntry
        {
            public string Path { get; set; } = "";
            public bool UseBigEndian { get; set; } = false;
            public override string ToString() => Path;
        }
    
        string Root => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string DefaultDb => System.IO.Path.Combine(Root, "Database", "Apollo");
        string DefaultTools => System.IO.Path.Combine(Root, "Tools");

        AppSettings settings = AppSettings.Load();

        SavepatchText? patch;
        string? patchPath;
                string? currentPatchText;
        string? dataPath;
        string? patcherPath;

        List<SavepatchText.CodeBlock> allCodes = new();

        public class PatchItem
        {
            public string Display { get; set; } = "";
            public string Path { get; set; } = "";
        }
        // Keeps an unfiltered copy of games for search/sort operations
        private readonly List<PatchItem> allGames = new List<PatchItem>();

        public MainWindow()
        {
            InitializeComponent();

try
{
    ModCodeWindowIntegration.AttachToCodesPanel(
    codesPanel,
    asBlock: item => item as SavepatchText.CodeBlock,
    getFullFileText: () => currentPatchText ?? string.Empty,
    replaceFirstTokenLine: (cb, newLine) =>
    {
        // IMPORTANT: do NOT rewrite any cb.Lines here.
        // The integration already updated the exact line that had the {MOD} token.
        // Just refresh the preview and the list visuals.

        try { ShowPreview(cb);

            // Attach non-invasive AMOUNT addin (appends to menu after base handler)
            try { SpecialAmountAddin.AttachToCodesPanel(codesPanel); } catch { }
 } catch { /* preview method exists in your file */ }

        try
        {
            var container = codesPanel.ItemContainerGenerator.ContainerFromItem(cb) as FrameworkElement;
            if (container != null)
            {
                var dc = container.DataContext;
                container.DataContext = null;
                container.DataContext = dc;
            }
            codesPanel.Items.Refresh();
        }
        catch { /* ignore */ }
    },
    appendAppliedName: (cb, disp) => { /* optional: decorate name if you want */ }
);
// ADD THIS ONE LINE *after* the base attach:
SpecialAmountAddin.AttachToCodesPanel(codesPanel);
}

catch { /* non-fatal hook */ }


            LoadSortMode();


            if (string.IsNullOrWhiteSpace(settings.DatabasePath)) settings.DatabasePath = DefaultDb;
            if (string.IsNullOrWhiteSpace(settings.ToolsPath)) settings.ToolsPath = DefaultTools;

            // cmbDb.Text = settings.DatabasePath!;  // now using ComboBoxItem with Tag
            txtTools.Text = settings.ToolsPath!;
            // Load available databases from Database\Database.Info or by scanning folders
            try
            {
                LoadDatabases();
                // If settings has a saved path, try to select it; otherwise select first
                if (!string.IsNullOrWhiteSpace(settings.DatabasePath))
                {
                    var match = cmbDb.Items.Cast<ComboBoxItem>().FirstOrDefault(it => string.Equals(it.Tag?.ToString(), settings.DatabasePath, StringComparison.OrdinalIgnoreCase));
                    if (match != null) cmbDb.SelectedItem = match;
                }
                if (cmbDb.SelectedIndex < 0 && cmbDb.Items.Count > 0)
                    cmbDb.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Failed to load databases: " + ex.Message, "Database", MessageBoxButton.OK, MessageBoxImage.Warning);
            }


            chkBackup.IsChecked = settings.BackupEnabled;

            if (!string.IsNullOrWhiteSpace(settings.PatcherPath) && File.Exists(settings.PatcherPath))
            {
                patcherPath = settings.PatcherPath;
            }
            else
            {
                var defaultPatcher = System.IO.Path.Combine(settings.ToolsPath!, "patcher.exe");
                if (File.Exists(defaultPatcher)) patcherPath = defaultPatcher;
            }
            txtPatcher.Text = patcherPath ?? "";

            RefreshDatabaseList();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                if (settings != null)
                {
                    if (cmbDb != null)
                        settings.DatabasePath = (cmbDb.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? cmbDb.Text;
                    if (txtTools != null) settings.ToolsPath = txtTools.Text;
                    if (txtPatcher != null) settings.PatcherPath = txtPatcher.Text;
                    if (chkBackup != null) settings.BackupEnabled = chkBackup.IsChecked == true;
                    settings.Save();
                if (collectorWin != null) { collectorWin.AllowRealClose(); collectorWin.Close(); }
                }
            }
            catch (System.Exception ex)
            {
                ApolloGUI.Utilities.CrashLogger.LogException("MainWindow.OnClosed", ex);
            }
        }

        void Exit_Click(object sender, System.Windows.RoutedEventArgs e) => Close();

        void Preferences_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(settings) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // display remains; selection binding uses ComboBoxItem Tag
                cmbDb.SelectedItem = cmbDb.Items.Cast<object>().OfType<ComboBoxItem>().FirstOrDefault(it => string.Equals((it as ComboBoxItem)?.Tag as string, settings.DatabasePath, StringComparison.OrdinalIgnoreCase)) ?? cmbDb.SelectedItem;
                txtTools.Text = settings.ToolsPath ?? txtTools.Text;
                ApplyFilter();
            }
        }

void About_Click(object sender, RoutedEventArgs e)
{
    // Look for Help/ApolloGUI_Help_v1.00.html next to the EXE
    var helpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help", "ApolloGUI_Help_v1.00.html");

    if (File.Exists(helpPath))
    {
        try
        {
            // Prefer showing help inside the app
            var w = new Window
            {
                Title = "Help — ApolloGUI v1.00",
                Width = 980,
                Height = 720,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var browser = new WebBrowser();
            w.Content = browser;
            browser.Navigate(new Uri(helpPath));
            w.Show();
            return; // done
        }
        catch
        {
            // Fallback: open with the system’s default browser
            try
            {
                Process.Start(new ProcessStartInfo(helpPath) { UseShellExecute = true });
                return;
            }
            catch (Exception ex2)
            {
                MessageBox.Show(this, $"Couldn't open Help.\n{ex2.Message}\n{helpPath}",
                    "Help", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Final fallback: original About text
    MessageBox.Show(this,
        "ApolloGUI backups\nBackups stored in %Root%\\Backups\\<GameName> as zip files.\nSettings saved to settings.json.",
        "About", MessageBoxButton.OK, MessageBoxImage.Information);
}

        void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }

        void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            foreach (var f in files)
            {
                var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                if (ext == ".savepatch") AddPatchToList(f, selectIt: true);
                else SetData(f);
            }
        }

        void DropPatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => BrowsePatch();
        void DropData_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => BrowseData();

        void Refresh_Click(object sender, System.Windows.RoutedEventArgs e) => RefreshDatabaseList();
        void AddPatch_Click(object sender, System.Windows.RoutedEventArgs e) => BrowsePatch(addToListOnly: true);

        void RefreshDatabaseList()
        {            allGames.Clear();

            lstGames.Items.Clear();
            allGames.Clear();
            var dbPath = GetSelectedDbPath();
            if (!Directory.Exists(dbPath))
            {
                Log($"> Database path not found: {dbPath}");
                return;
            }

            var searchOpt = chkRecurse.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            IEnumerable<string> files = Array.Empty<string>();
            try { files = Directory.EnumerateFiles(dbPath, "*.savepatch", searchOpt); }
            catch (Exception ex) { Log("[!] " + ex.Message); }

            foreach (var f in files) AddPatchToList(f);
            Log($"> Loaded {lstGames.Items.Count} savepatch file(s) from database.");
        
            ApplyGamesSortAndFilter();
        }

        void AddPatchToList(string path, bool selectIt = false)
        {
            try
            {
                var sp = SavepatchText.Load(path);
                var disp = $"{sp.Title ?? System.IO.Path.GetFileNameWithoutExtension(path)} ({sp.Cusa ?? "?"})";
                var item = new PatchItem { Display = disp, Path = path };
                lstGames.Items.Add(item);
                if (selectIt) { lstGames.SelectedItem = item; lstGames.ScrollIntoView(item); }
            }
            catch (Exception ex)
            {
                Log("[!] Failed to parse: " + path + " — " + ex.Message);
            }
        }

        void LstGames_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lstGames.SelectedItem is PatchItem item) LoadPatch(item.Path);
        }

        void BrowsePatch(bool addToListOnly = false)
        {
            var start = Directory.Exists(GetSelectedDbPath()) ? GetSelectedDbPath() : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Savepatch (*.savepatch)|*.savepatch|All files (*.*)|*.*", Title = "Open .savepatch", InitialDirectory = start };
            if (ofd.ShowDialog() == true)
            {
                if (addToListOnly) AddPatchToList(ofd.FileName, selectIt: true);
                else LoadPatch(ofd.FileName);
            }
        }
        void BrowseData()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Binary (*.bin;*.dat;*.*)|*.bin;*.dat;*.*", Title = "Select data file (.bin)" };
            if (ofd.ShowDialog() == true) SetData(ofd.FileName);
        }
        void BrowsePatcher_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var start = Directory.Exists(txtTools.Text) ? txtTools.Text : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*", Title = "Select patcher.exe", InitialDirectory = start };
            if (ofd.ShowDialog() == true) { patcherPath = ofd.FileName; txtPatcher.Text = patcherPath; }
        }

        void LoadPatch(string path)
        {
            try
            {
                patchPath = path;
                lblPatchPath.Text = path;
                currentPatchText = System.IO.File.ReadAllText(path);
                CheatFileContext.CurrentText = currentPatchText; // keep converters in sync
                patch = SavepatchText.Load(path);
                ResetCollectorForNewPatch();
                txtGame.Text = $"Game: {patch?.Title ?? "(unknown)"}  ({patch?.Cusa ?? "?"})";

                allCodes = patch?.Codes?.ToList() ?? new List<SavepatchText.CodeBlock>();
                UpdateMetadataVisibility();
                ApplyFilter();

                txtPreview.Clear();
                lblSelected.Text = "(select a code)";
                Log($"> Loaded .savepatch: {path}");
                UpdateSelectionText();
				
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        void UpdateMetadataVisibility()
        {
            if (settings.ShowMetadata && patch != null)
            {
                grpMeta.Visibility = System.Windows.Visibility.Visible;
                txtMeta.Text = string.Join(Environment.NewLine, patch.Metadata);
            }
            else
            {
                grpMeta.Visibility = System.Windows.Visibility.Collapsed;
                txtMeta.Text = string.Empty;
            }
        }

        void ApplyFilter()
        {
            var q = ((this.FindName("txtSearch") as System.Windows.Controls.TextBox)?.Text ?? string.Empty).Trim();
            IEnumerable<SavepatchText.CodeBlock> view = allCodes;
            if (!string.IsNullOrWhiteSpace(q))
            {
                view = view.Where(c => (c.Name?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                                     || c.Lines.Any(l => l.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            
var list = view.ToList();
try
{
    ModCodeWindowIntegration.ApplyModPrefixes(
        list,
        item => ((SavepatchText.CodeBlock)item).Lines.FirstOrDefault(l => System.Text.RegularExpressions.Regex.IsMatch(l ?? string.Empty, "\\{[A-Za-z0-9_]+\\}")) ?? string.Empty,
        item => ((SavepatchText.CodeBlock)item).Name,
        (item, name) => ((SavepatchText.CodeBlock)item).Name = name ?? string.Empty,
        currentPatchText ?? string.Empty
    );
}
catch { /* optional */ }

// Hide the [MODS:] header in the Codes window
var visibleBlocks = (list ?? Enumerable.Empty<SavepatchText.CodeBlock>())
    .Where(b => !string.Equals(b?.Name?.Trim(), "MODS:", StringComparison.OrdinalIgnoreCase))
    .ToList();

codesPanel.ItemsSource = null;            // clear first to force refresh
codesPanel.ItemsSource = visibleBlocks;   // assign filtered list

        }

        void SetData(string path)
        {
            dataPath = path;
            lblDataPath.Text = path;
            Log($"> Target file: {path}");
        }

        void CodeCheckChanged(object sender, System.Windows.RoutedEventArgs e)
{
    // keep your selection count text in sync
    UpdateSelectionText();

    // when a box is checked, show that code in the preview pane
    if (sender is System.Windows.Controls.CheckBox cb
        && cb.IsChecked == true
        && cb.DataContext is SavepatchText.CodeBlock code)
    {
        ShowPreview(code);
    }
}


        void Code_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is SavepatchText.CodeBlock code) ShowPreview(code);
        }
        void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

        void SelectAll_Click(object sender, System.Windows.RoutedEventArgs e) => SetAllChecks(true);
        void SelectNone_Click(object sender, System.Windows.RoutedEventArgs e) => SetAllChecks(false);
        void Invert_Click(object sender, System.Windows.RoutedEventArgs e) => InvertChecks();

        void SetAllChecks(bool value)
        {
            foreach (var obj in codesPanel.Items)
            {
                var cont = (System.Windows.Controls.ContentPresenter)codesPanel.ItemContainerGenerator.ContainerFromItem(obj);
                if (cont == null) continue;
                var cbox = FindVisualChild<System.Windows.Controls.CheckBox>(cont);
                if (cbox != null) cbox.IsChecked = value;
            }
            UpdateSelectionText();
        }
        void InvertChecks()
        {
            foreach (var obj in codesPanel.Items)
            {
                var cont = (System.Windows.Controls.ContentPresenter)codesPanel.ItemContainerGenerator.ContainerFromItem(obj);
                if (cont == null) continue;
                var cbox = FindVisualChild<System.Windows.Controls.CheckBox>(cont);
                if (cbox != null) cbox.IsChecked = !(cbox.IsChecked == true);
            }
            UpdateSelectionText();
        }

        void UpdateSelectionText()
        {
            var idxs = new List<int>();
            foreach (var obj in codesPanel.Items)
            {
                if (obj is SavepatchText.CodeBlock cb)
                {
                    var cont = (System.Windows.Controls.ContentPresenter)codesPanel.ItemContainerGenerator.ContainerFromItem(cb);
                    if (cont == null) continue;
                    var cbox = FindVisualChild<System.Windows.Controls.CheckBox>(cont);
                    if (cbox != null && cbox.IsChecked == true) idxs.Add(cb.Index);
                }
            }
            idxs.Sort();
            txtSelection.Text = CompressIndices(idxs);
        }

        static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                var sub = FindVisualChild<T>(child);
                if (sub != null) return sub;
            }
            return null;
        }

        string CompressIndices(List<int> idxs)
        {
            if (idxs.Count == 0) return "";
            var parts = new List<string>();
            int start = idxs[0], prev = idxs[0];
            for (int i = 1; i < idxs.Count; i++)
            {
                if (idxs[i] == prev + 1) { prev = idxs[i]; continue; }
                parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
                start = prev = idxs[i];
            }
            parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
            return string.Join(",", parts);
        }

        void ShowPreview(SavepatchText.CodeBlock cb)
        {
            lblSelected.Text = $"[{cb.Name}]  (#{cb.Index})";
            var sb = new StringBuilder();
            foreach (var ln in cb.Lines) sb.AppendLine(ln);
            txtPreview.Text = sb.ToString();
        }

        async void Run_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtPatcher.Text) || !File.Exists(txtPatcher.Text)) { System.Windows.MessageBox.Show(this, "Select patcher.exe", "Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }
                if (patchPath == null || !File.Exists(patchPath)) { System.Windows.MessageBox.Show(this, "Open a .savepatch file", "Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }
                if (dataPath == null || !File.Exists(dataPath)) { System.Windows.MessageBox.Show(this, "Select a target .bin file", "Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }

                var gameName = patch?.Title ?? "UnknownGame";
                var modName = System.IO.Path.GetFileNameWithoutExtension(dataPath) ?? "data";

                if (chkBackup.IsChecked == true)
                {
                    try
                    {
                        var zip = BackupManager.CreateBackupZip(settings, Root, gameName, dataPath);
                        Log($"> Backup created: {zip}");
                    }
                    catch (Exception bx)
                    {
                        Log("[!] Backup failed: " + bx.Message);
                    }
                }

                var patcherPathLocal = txtPatcher.Text;
                var dbEntry = GetSelectedDbEntry();
                if (dbEntry != null)
                {
                    var toolsRoot = settings.ToolsPath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
                    var big = System.IO.Path.Combine(toolsRoot, "patcher-bigendian.exe");
                    var def = System.IO.Path.Combine(toolsRoot, "patcher.exe");
                    if (dbEntry.UseBigEndian && File.Exists(big)) patcherPathLocal = big;
                    else if (!dbEntry.UseBigEndian && File.Exists(def)) patcherPathLocal = def;
                }
                var selection = txtSelection.Text?.Trim();
                if (string.IsNullOrWhiteSpace(selection)) { System.Windows.MessageBox.Show(this, "Select at least one code.", "No selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }

                Log($"> Running: {patcherPathLocal}");
                Log($"> Args: {System.IO.Path.GetFileName(patchPath)} {selection} {System.IO.Path.GetFileName(dataPath)}");

                var result = await PatcherRunner.RunAsync(
                    patcherExePath: patcherPathLocal,
                    savepatchPath: patchPath!,
                    selection: selection,
                    dataFilePath: dataPath,
                    workingDirectory: Root
                );

                if (!string.IsNullOrWhiteSpace(result.StdOut)) Log(result.StdOut.Trim());
                if (!string.IsNullOrWhiteSpace(result.StdErr)) Log("[stderr] " + result.StdErr.Trim());
                Log($"> Exit: {result.ExitCode} {(result.Success ? "(OK)" : "(FAILED)")}");
                if (!result.Success) System.Windows.MessageBox.Show(this, result.StdErr, "Patcher failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        void RestoreBackup_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (patch == null || string.IsNullOrWhiteSpace(dataPath)) { System.Windows.MessageBox.Show(this, "Load a .savepatch and select a target file first.", "Restore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information); return; }

                var gameFolder = BackupManager.EnsureGameFolder(settings, Root, patch.Title ?? "UnknownGame");
                var modName = System.IO.Path.GetFileNameWithoutExtension(dataPath!) ?? "data";

                var dlg = new RestoreWindow(gameFolder, modName, dataPath!){ Owner = this };
                if (dlg.ShowDialog() == true && dlg.SelectedZipPath != null)
                {
                    var zip = dlg.SelectedZipPath;
                    try
                    {
                        using var zp = System.IO.Compression.ZipFile.OpenRead(zip);
                        var entry = zp.Entries.FirstOrDefault();
                        if (entry == null) throw new InvalidOperationException("Zip is empty.");
                        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), entry.Name);
                        ApolloGUI.Utilities.ZipUtils.ExtractToFile(entry, temp, true);
                        System.IO.File.Copy(temp, dataPath!, true);
                        Log($"> Restored backup: {zip} -> {dataPath}");
                        System.Windows.MessageBox.Show(this, "Backup restored.", "Restore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception rx)
                    {
                        System.Windows.MessageBox.Show(this, "Restore failed: " + rx.Message, "Restore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Restore", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        void OpenBackups_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var root = BackupManager.GetBackupsRoot(settings, Root);
                Directory.CreateDirectory(root);
                Process.Start(new ProcessStartInfo { FileName = root, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.Message, "Backups", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        void ClearLog_Click(object sender, System.Windows.RoutedEventArgs e) => txtLog.Clear();
        void Log(string msg) => txtLog.AppendText(msg + Environment.NewLine);
    

void CmbDb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDb == null || cmbDb.SelectedItem == null) return;

            var sel = GetSelectedDbPath();
            settings.DatabasePath = sel;

            // Auto-refresh games list
            try
            {
                RefreshDatabaseList();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Failed to refresh database: " + ex.Message,
                    "Database",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

void ReloadDatabaseIfAvailable()
        {
            try
            {
                RefreshDatabaseList();
            }
            catch
            {
                // ignore
            }
        }

        void LoadDatabases()

        {
            cmbDb.Items.Clear();
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string dbRoot = System.IO.Path.Combine(root, "Database");
            string infoPath = System.IO.Path.Combine(dbRoot, "Database.Info");

            var entries = new List<(string Path, bool UseBig)>();

            if (File.Exists(infoPath))
            {
                foreach (var raw in File.ReadAllLines(infoPath))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//")) continue;
                    bool useBig = false;
                    if (line.StartsWith("^")) { useBig = true; line = line.Substring(1).TrimStart(); }
                    var parts = line.Split(new[] {'\\', '/'}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var device = parts[0];
                        var system = parts[1];
                        var full = System.IO.Path.Combine(dbRoot, device, system);
                        if (Directory.Exists(full))
                            entries.Add((full, useBig));
                    }
                }
            }

            if (entries.Count == 0)
            {
                // Fallback: scan Database\<Device>\<System>
                if (Directory.Exists(dbRoot))
                {
                    foreach (var deviceDir in Directory.GetDirectories(dbRoot))
                    {
                        foreach (var sysDir in Directory.GetDirectories(deviceDir))
                        {
                            entries.Add((sysDir, false));
                        }
                    }
                }
            }

            // Build ComboBox items with display 'Database\<Device>\<System>' and Tag = DbEntry
            foreach (var e in entries
                .Distinct()
                .OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string device = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(e.Path)) ?? "";
                    string system = System.IO.Path.GetFileName(e.Path) ?? "";
                    string display = System.IO.Path.Combine("Database", device, system).Replace(System.IO.Path.DirectorySeparatorChar, '\\');
                    cmbDb.Items.Add(new ComboBoxItem { Content = display, Tag = new DbEntry { Path = e.Path, UseBigEndian = e.UseBig } });
                }
                catch
                {
                    cmbDb.Items.Add(new ComboBoxItem { Content = e.Path, Tag = new DbEntry { Path = e.Path, UseBigEndian = e.UseBig } });
                }
            }
        }

DbEntry? GetSelectedDbEntry()
{
    if (cmbDb?.SelectedItem is ComboBoxItem cbi)
        return cbi.Tag as DbEntry;
    return null;
}

string GetSelectedDbPath()
{
    var entry = GetSelectedDbEntry();
    if (entry != null) return entry.Path;
    return string.IsNullOrWhiteSpace(cmbDb?.Text) ? "" : cmbDb.Text;
}

// Centralized patcher resolver
string ResolvePatcherPath(bool updateUi = true)
{
    try
    {
        var entry = GetSelectedDbEntry();
        var toolsRoot = (txtTools.Tag as string) ?? (settings.ToolsPath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools"));

        var big = System.IO.Path.Combine(toolsRoot, "patcher-bigendian.exe");
        var dfl = System.IO.Path.Combine(toolsRoot, "patcher.exe");

        string candidate = null;
        if (entry != null && entry.UseBigEndian && System.IO.File.Exists(big)) candidate = big;
        else if (System.IO.File.Exists(dfl)) candidate = dfl;
        else if (!string.IsNullOrWhiteSpace(patcherPath) && System.IO.File.Exists(patcherPath)) candidate = patcherPath;
        else if (!string.IsNullOrWhiteSpace(settings.PatcherPath) && System.IO.File.Exists(settings.PatcherPath)) candidate = settings.PatcherPath;

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            patcherPath = candidate;
            if (updateUi) txtPatcher.Text = System.IO.Path.GetFileName(candidate);
            return candidate;
        }
    }
    catch { }
    return "";
}

void OpenInNotepad_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var mi = sender as System.Windows.Controls.MenuItem;
                var item = mi?.CommandParameter as PatchItem ?? lstGames.SelectedItem as PatchItem;
                var path = item?.Path;
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                {
                    System.Windows.MessageBox.Show(this, "No file found for this game entry.", "Open in Notepad",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, "Failed to open in Notepad: " + ex.Message, "Open in Notepad",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }



void Code_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    try
    {
        if (e.ClickCount >= 2)
        {
            Code_DoubleClick(sender, e);
            e.Handled = true;
        }
    }
    catch (System.Exception ex)
    {
        Log("[!] Collector double-click handler failed: " + ex.Message);
    }
}


void ResetCollectorForNewPatch()
{
    try
    {
        if (collectorWin != null)
        {
            collectorWin.ClearAll();
        }
    }
    catch (System.Exception ex)
    {
        Log("[!] ResetCollectorForNewPatch failed: " + ex.Message);
    }
}

// === Collector integration ===
CollectorWindow? collectorWin;

public (string? cusa, string? platform, string? title, string? source) GetCurrentPatchMetadata()
{
    return (patch?.Cusa, patch?.Platform ?? "PS4", patch?.Title, patch?.Source ?? "ApolloGUI");
}

void OpenCollector_Click(object sender, System.Windows.RoutedEventArgs e)
{
    if (collectorWin == null || !collectorWin.IsLoaded)
    {
        collectorWin = new CollectorWindow(this){ Owner = this };
    }
    collectorWin.Show(); collectorWin.Activate();
}

void Code_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    try
    {
        if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is SavepatchText.CodeBlock code)
        {
            var name = code.Name ?? "(unnamed)";
            var text = string.Join("\n", code.Lines ?? new System.Collections.Generic.List<string>());
            if (collectorWin == null || !collectorWin.IsLoaded) collectorWin = new CollectorWindow(this){ Owner = this };
            collectorWin.Show(); collectorWin.Activate();
            collectorWin.AddOrUpdate(name, text);
            Log($"> Added to Collector: [{name}]");
        }
    }
    catch (Exception ex)
    {
        Log("[!] Collector add failed: " + ex.Message);
    }
}

// Build temp.savepatch and run patcher immediately
public async System.Threading.Tasks.Task RunCollectorPatchAsync(string tempSavepatchPath, int totalBlocks)
{
    try
    {
        if (string.IsNullOrWhiteSpace(txtPatcher.Text)) { System.Windows.MessageBox.Show(this, "Select a patcher exe first.", "Patch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }
        if (string.IsNullOrWhiteSpace(dataPath) || !System.IO.File.Exists(dataPath)) { System.Windows.MessageBox.Show(this, "Select a data file first.", "Patch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning); return; }
        // Backup when running from Collector (uses global Settings toggle)
        if (settings?.BackupEnabled == true)
        {
            try
            {
                var gameName = patch?.Title ?? "UnknownGame";
                var zip = BackupManager.CreateBackupZip(settings, Root, gameName, dataPath!);
                Log($"> Backup created: {zip}");
            }
            catch (System.Exception bx)
            {
                Log("[!] Backup failed: " + bx.Message);
            }
        }



        // Choose endian-specific patcher if DB entry requires
        var patcherPathLocal = txtPatcher.Text;
        var dbEntry = GetSelectedDbEntry();
        if (dbEntry != null)
        {
            var toolsRoot = settings.ToolsPath ?? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
            var big = System.IO.Path.Combine(toolsRoot, "patcher-bigendian.exe");
            var def = System.IO.Path.Combine(toolsRoot, "patcher.exe");
            if (dbEntry.UseBigEndian && System.IO.File.Exists(big)) patcherPathLocal = big;
            else if (!dbEntry.UseBigEndian && System.IO.File.Exists(def)) patcherPathLocal = def;
        }

        // Selection: apply all blocks 1..N
        var selection = $"1-{totalBlocks}";

        Log($"> Running (collector): {patcherPathLocal}");
        Log($"> Args: {System.IO.Path.GetFileName(tempSavepatchPath)} {selection} {System.IO.Path.GetFileName(dataPath)}");

        var result = await PatcherRunner.RunAsync(
            patcherExePath: patcherPathLocal,
            savepatchPath: tempSavepatchPath,
            selection: selection,
            dataFilePath: dataPath,
            workingDirectory: Root
        );

        if (!string.IsNullOrWhiteSpace(result.StdOut)) Log(result.StdOut.Trim());
        if (!string.IsNullOrWhiteSpace(result.StdErr)) Log("[stderr] " + result.StdErr.Trim());
        Log($"> Exit: {result.ExitCode} {(result.Success ? "(OK)" : "(FAILED)")}");
        if (!result.Success) System.Windows.MessageBox.Show(this, "Patcher failed. See log for details.", "Patch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
    catch (Exception ex)
    {
        System.Windows.MessageBox.Show(this, ex.Message, "Collector Patch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
}
}




        private static string ExtractCusa(string display)
        {
            if (string.IsNullOrEmpty(display)) return display ?? string.Empty;
            int l = display.LastIndexOf('(');
            int r = display.LastIndexOf(')');
            if (l >= 0 && r > l) return display.Substring(l + 1, r - l - 1);
            return display;
        }
    

    
        private static System.Collections.Generic.IEnumerable<System.Windows.Controls.CheckBox> FindChildCheckBoxes(System.Windows.DependencyObject parent)
        {
            if (parent == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.CheckBox c) yield return c;
                foreach (var sub in FindChildCheckBoxes(child)) yield return sub;
            }
        }

        private void MoveCheckedToCollector_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (collectorWin == null || !collectorWin.IsLoaded)
                    collectorWin = new CollectorWindow(this) { Owner = this };
                collectorWin.Show();
                collectorWin.Activate();

                int moved = 0, skipped = 0;
                var skippedList = new System.Collections.Generic.List<string>();

                foreach (var cb in FindChildCheckBoxes(codesPanel))
                {
                    if (cb.IsChecked != true) continue;
                    if (cb.DataContext is not SavepatchText.CodeBlock code) continue;

                    var name = code.Name ?? "(unnamed)";
                    if (HasUnresolvedTokens(code, out var _missing))
                    {
                        skipped++;
                        if (!string.IsNullOrWhiteSpace(_missing))
                            skippedList.Add($"{name}: {_missing}");
                        continue;
                    }

                    var text = string.Join("\n", code.Lines ?? new System.Collections.Generic.List<string>());
                    collectorWin.AddOrUpdate(name, text);
                    moved++;
                }

                if (moved > 0) Log($"> Moved {moved} code(s) to Collector.");
                if (skipped > 0)
                {
                    var msg = "Skipped " + skipped + " code(s) with unresolved tokens.";
                    if (skippedList.Count > 0) msg += "\n\n" + string.Join("\n", skippedList);
                    System.Windows.MessageBox.Show(this, msg, "Collector", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (System.Exception ex)
            {
                Log("[!] Move Checked failed: " + ex.Message);
            }
        }


        private void ResetAllToDatabase_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                if (codesPanel?.ItemsSource is System.Collections.IEnumerable srcEnum)
                {
                    var list = new System.Collections.Generic.List<SavepatchText.CodeBlock>();
                    foreach (var it in srcEnum)
                        if (it is SavepatchText.CodeBlock cbItem) list.Add(cbItem);

                    int count = 0;
                    foreach (var cb in list)
                    {
                        if (NormalizeAndResetCodeFromDatabase(cb)) count++;
                    }

                    try
                    {
                        ModCodeWindowIntegration.ApplyModPrefixes(
                            list,
                            item => ((SavepatchText.CodeBlock)item).Lines.FirstOrDefault(l => System.Text.RegularExpressions.Regex.IsMatch(l ?? string.Empty, "\\{[A-Za-z0-9_]+\\}")) ?? string.Empty,
                            item => ((SavepatchText.CodeBlock)item).Name,
                            (item, name) => ((SavepatchText.CodeBlock)item).Name = name ?? string.Empty,
                            currentPatchText ?? string.Empty
                        );
                    }
                    catch { /* optional */ }

                    var visibleBlocks = (list ?? System.Linq.Enumerable.Empty<SavepatchText.CodeBlock>())
                        .Where(b => !string.Equals(b?.Name?.Trim(), "MODS:", System.StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    codesPanel.ItemsSource = null;
                    codesPanel.ItemsSource = visibleBlocks;

                    UpdateSelectionText();
                    Log($"> Reset {count} code(s) to Database.");
                }
            }
            catch (System.Exception ex)
            {
                Log("[!] Reset All failed: " + ex.Message);
            }
        }
    

        private static string NormalizeHeaderForLookup(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var s = name.Trim();
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s*\(.*\)$", "");
            s = s.Replace("-M-", "", System.StringComparison.OrdinalIgnoreCase)
                 .Replace("{MOD}", "", System.StringComparison.OrdinalIgnoreCase)
                 .Replace("{/MOD}", "", System.StringComparison.OrdinalIgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        private static bool TryGetDatabaseBlockByHeader(string fileText, string headerName, out string dbHeader, out System.Collections.Generic.List<string> dbLines)
        {
            dbHeader = string.Empty;
            dbLines = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(fileText)) return false;
            var normTarget = NormalizeHeaderForLookup(headerName);

            string? currentHeaderNorm = null;
            string? currentHeaderRaw = null;
            var currentLines = new System.Collections.Generic.List<string>();

            foreach (var raw in fileText.Replace("\r", "").Split('\n'))
            {
                var line = raw ?? string.Empty;
                var m = System.Text.RegularExpressions.Regex.Match(line, @"^\s*\[([^\]]+)\]\s*$");
                if (m.Success)
                {
                    // Before starting a new block, check if the previous block matches
                    if (currentHeaderNorm != null &&
                        string.Equals(currentHeaderNorm, normTarget, System.StringComparison.OrdinalIgnoreCase))
                    {
                        dbHeader = currentHeaderRaw ?? currentHeaderNorm;
                        dbLines = new System.Collections.Generic.List<string>(currentLines);
                        return true;
                    }

                    // Start a new block
                    currentHeaderRaw = m.Groups[1].Value.Trim();
                    currentHeaderNorm = NormalizeHeaderForLookup(currentHeaderRaw);
                    currentLines.Clear();
                }
                else
                {
                    if (currentHeaderNorm != null)
                    {
                        currentLines.Add(line);
                    }
                }
            }

            // End of file: check the last block
            if (currentHeaderNorm != null &&
                string.Equals(currentHeaderNorm, normTarget, System.StringComparison.OrdinalIgnoreCase))
            {
                dbHeader = currentHeaderRaw ?? currentHeaderNorm;
                dbLines = new System.Collections.Generic.List<string>(currentLines);
                return true;
            }

            return false;
        }

        private bool NormalizeAndResetCodeFromDatabase(SavepatchText.CodeBlock cb)
        {
            if (cb == null) return false;
            if (TryGetDatabaseBlockByHeader(currentPatchText ?? string.Empty, cb.Name ?? string.Empty,
                                            out var dbHeader, out var dbLines))
            {
                cb.Name = dbHeader;
                cb.Lines = new System.Collections.Generic.List<string>(dbLines);
                return true;
            }
            return false;
        }
    

        private static readonly System.Text.RegularExpressions.Regex UnresolvedTokenRegex =
            new System.Text.RegularExpressions.Regex(@"\{(Amount:[^}]+|[A-Za-z0-9_]+)\}", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static bool HasUnresolvedTokens(SavepatchText.CodeBlock cb, out string details)
        {
            var found = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var line in (cb.Lines ?? new System.Collections.Generic.List<string>()))
            {
                var t = (line ?? string.Empty);
                foreach (System.Text.RegularExpressions.Match m in UnresolvedTokenRegex.Matches(t))
                {
                    if (m.Success)
                    {
                        var tok = m.Groups[1].Value?.Trim() ?? string.Empty;
                        if (tok.Length > 0) found.Add(tok);
                    }
                }
            }
            if (found.Count > 0)
            {
                details = string.Join(", ", found);
                return true;
            }
            details = string.Empty;
            return false;
        }

        private void CopyCode_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var fe = sender as System.Windows.FrameworkElement;
                var dc = fe?.DataContext;
                string text = null;
                if (dc != null)
                {
                    var t = dc.GetType();
                    var p = t.GetProperty("Name") ?? t.GetProperty("Display") ?? t.GetProperty("ToString");
                    if (p != null && p.Name != "ToString")
                        text = p.GetValue(dc)?.ToString();
                    if (string.IsNullOrEmpty(text))
                        text = dc.ToString();
                }
                if (!string.IsNullOrEmpty(text))
                    System.Windows.Clipboard.SetText(text);
            }
            catch { /* no-op */ }
        }
   } 
}