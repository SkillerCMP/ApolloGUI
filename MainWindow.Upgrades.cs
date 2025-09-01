// ============================================================================
//  ApolloGUI — MainWindow.Upgrades.cs
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ApolloGUI
{
    public partial class MainWindow : Window
    {
        private FileSystemWatcher? _dbWatch;

        // Optional: call this from your constructor or Loaded event to activate runtime helpers.
        private void MainWindow_Upgrades_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Attach log auto-scroll if the log textbox exists
                if (this.FindName("txtLog") is TextBox log)
                {
                    log.TextChanged -= Log_TextChanged_AutoScroll;
                    log.TextChanged += Log_TextChanged_AutoScroll;
                }

                // Hook txtFilter or txtSearch to quick-find behavior (non-destructive)
                if (this.FindName("txtFilter") is TextBox tf)
                    tf.TextChanged += TxtFilter_TextChanged;
                else if (this.FindName("txtSearch") is TextBox ts)
                    ts.TextChanged += TxtFilter_TextChanged;

                // Add "Open" buttons (DB/Tools) at runtime if their parents are panels
                TryInsertOpenButtons();

                // Attach watcher for *.savepatch changes
                AttachDbWatcher();
            }
            catch (Exception ex) { LogSafe("[!] Upgrades_Loaded: " + ex.Message); }
        }

        private void Log_TextChanged_AutoScroll(object? sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is TextBox tb) tb.ScrollToEnd();
            }
            catch { }
        }

        private void LogSafe(string msg)
        {
            try
            {
                if (this.FindName("txtLog") is TextBox log)
                {
                    log.AppendText(msg + Environment.NewLine);
                    log.ScrollToEnd();
                }
                else
                {
                    Debug.WriteLine(msg);
                }
            }
            catch { Debug.WriteLine(msg); }
        }

        // Lightweight filter: selects the first matching item in lstGames
        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var box = sender as TextBox;
                var q = (box?.Text ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(q)) return;

                if (this.FindName("lstGames") is ListBox lb && lb.Items.Count > 0)
                {
                    for (int i = 0; i < lb.Items.Count; i++)
                    {
                        var item = lb.Items[i];
                        var s = item?.ToString()?.ToLowerInvariant() ?? string.Empty;
                        if (s.Contains(q)) { lb.SelectedIndex = i; lb.ScrollIntoView(item); break; }
                    }
                }
            }
            catch (Exception ex) { LogSafe("[!] Filter: " + ex.Message); }
        }

        private void AttachDbWatcher()
        {
            try
            {
                _dbWatch?.Dispose();
                var path = ResolveSelectedDbPath();
                if (!Directory.Exists(path)) return;

                _dbWatch = new FileSystemWatcher(path, "*.savepatch");
                _dbWatch.IncludeSubdirectories = TryGetRecurse();
                _dbWatch.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                _dbWatch.Created += (_, __) => Dispatcher.Invoke(TryRefreshDatabaseList);
                _dbWatch.Deleted += (_, __) => Dispatcher.Invoke(TryRefreshDatabaseList);
                _dbWatch.Renamed += (_, __) => Dispatcher.Invoke(TryRefreshDatabaseList);
                _dbWatch.Changed += (_, __) => Dispatcher.Invoke(TryRefreshDatabaseList);
                _dbWatch.EnableRaisingEvents = true;
                LogSafe("> Watching database: " + path);
            }
            catch (Exception ex) { LogSafe("[!] DB watcher: " + ex.Message); }
        }

        private void TryRefreshDatabaseList()
        {
            try
            {
                var mi = GetType().GetMethod("RefreshDatabaseList",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                mi?.Invoke(this, null);
            }
            catch (Exception ex) { LogSafe("[!] RefreshDatabaseList invoke: " + ex.Message); }
        }

        private bool TryGetRecurse()
        {
            try
            {
                if (this.FindName("chkRecurse") is CheckBox cb) return cb.IsChecked == true;
            } catch { }
            return false;
        }

        private string ResolveSelectedDbPath()
        {
            try
            {
                var mi = GetType().GetMethod("GetSelectedDbPath",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (mi != null)
                {
                    var res = mi.Invoke(this, null) as string;
                    if (!string.IsNullOrWhiteSpace(res)) return res!;
                }

                if (this.FindName("cmbDb") is ComboBox cmb)
                {
                    if (cmb.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag && Directory.Exists(tag)) return tag;
                    if (cmb.SelectedValue is string sv && Directory.Exists(sv)) return sv;
                    if (!string.IsNullOrWhiteSpace(cmb.Text) && Directory.Exists(cmb.Text)) return cmb.Text;
                }
            }
            catch { }
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database");
        }

        private void TryInsertOpenButtons()
        {
            try
            {
                if (this.FindName("cmbDb") is ComboBox cmb)
                {
                    if (VisualTreeHelper.GetParent(cmb) is Panel parent)
                    {
                        var btnDb = new Button { Content = "Open", Margin = new Thickness(6, 0, 0, 0) };
                        btnDb.Click += OpenDb_Click;
                        var idx = parent.Children.IndexOf(cmb);
                        if (idx >= 0) parent.Children.Insert(idx + 1, btnDb);
                    }
                }
            }
            catch (Exception ex) { LogSafe("[!] Insert OpenDb: " + ex.Message); }

            try
            {
                if (this.FindName("txtTools") is TextBox tools)
                {
                    if (VisualTreeHelper.GetParent(tools) is Panel parent)
                    {
                        var btnTools = new Button { Content = "Open", Margin = new Thickness(6, 0, 0, 0) };
                        btnTools.Click += OpenTools_Click;
                        var idx = parent.Children.IndexOf(tools);
                        if (idx >= 0) parent.Children.Insert(idx + 1, btnTools);
                    }
                }
            }
            catch (Exception ex) { LogSafe("[!] Insert OpenTools: " + ex.Message); }
        }

        private void OpenDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = ResolveSelectedDbPath();
                if (Directory.Exists(p))
                {
                    var psi = new ProcessStartInfo(p) { UseShellExecute = true };
                    Process.Start(psi);
                }
            }
            catch (Exception ex) { LogSafe("[!] OpenDb_Click: " + ex.Message); }
        }

        private void OpenTools_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? p = null;
                if (this.FindName("txtTools") is TextBox tools && !string.IsNullOrWhiteSpace(tools.Text)) p = tools.Text;
                if (string.IsNullOrWhiteSpace(p))
                    p = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools");
                if (Directory.Exists(p))
                {
                    var psi = new ProcessStartInfo(p) { UseShellExecute = true };
                    Process.Start(psi);
                }
                else LogSafe("> Tools folder not found: " + p);
            }
            catch (Exception ex) { LogSafe("[!] OpenTools_Click: " + ex.Message); }
        }
    }
}