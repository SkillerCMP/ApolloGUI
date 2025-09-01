// ============================================================================
//  ApolloGUI — MainWindow.GamesSort.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

// === MainWindow.GamesSort.cs ===
// Restores compact Search + two-state Sort (Name/ID) for Games list,
// and ensures clearing search shows ALL games again.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; // ToggleButton
using System.Windows.Data;

namespace ApolloGUI  // <-- CHANGE to your namespace if needed
{
    public partial class MainWindow : Window
    {
        // Snapshot of ALL games (always filter/sort from this, not from current UI list)
        private readonly List<PatchItem> _allGames = new List<PatchItem>();
        private bool _isFiltering;

        // Lookups (avoid compile-time dependency on x:Name fields)
        ToggleButton? SortToggle => this.FindName("tglSortById") as ToggleButton;
        TextBox?     GameSearch => this.FindName("txtGameSearch") as TextBox;

        // Keep _allGames in sync whenever the app repopulates lstGames
        void SetupGamesCollectionSync()
        {
            if (lstGames == null) return;
            if (lstGames.Items is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged -= LstGames_CollectionChanged;
                incc.CollectionChanged += LstGames_CollectionChanged;
            }
        }

        void LstGames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isFiltering) return; // ignore our own refills
            _allGames.Clear();
            foreach (var o in lstGames.Items)
                if (o is PatchItem pi) _allGames.Add(pi);
        }

        // XAML hooks
        void TxtGameSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || lstGames == null) return;
            ApplyGamesSortAndFilter();
        }

        void SortToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || lstGames == null) return;
            if (SortToggle != null)
                SortToggle.Content = (SortToggle.IsChecked == true) ? "Sort: ID" : "Sort: Name";
            SaveSortMode();
            ApplyGamesSortAndFilter();
        }

        // Filter + sort (always from _allGames)
        void ApplyGamesSortAndFilter()
        {
            if (lstGames == null) return;

            // Prime snapshot if first run
            if (_allGames.Count == 0 && lstGames.Items.Count > 0)
            {
                foreach (var o in lstGames.Items)
                    if (o is PatchItem pi) _allGames.Add(pi);
            }

            var toggle = SortToggle;
            var search = GameSearch;

            string q = search?.Text ?? string.Empty;
            bool hasQ = !string.IsNullOrWhiteSpace(q);

            List<PatchItem> filtered;
            if (hasQ)
            {
                filtered = new List<PatchItem>(_allGames.Count);
                foreach (var pi in _allGames)
                {
                    var name = GetName(pi);
                    var id   = GetId(pi);
                    if ((name?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (id?.IndexOf(q,   StringComparison.OrdinalIgnoreCase) >= 0)
                        || (pi.Display?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                        filtered.Add(pi);
                }
            }
            else
            {
                // Empty query -> ALL games
                filtered = new List<PatchItem>(_allGames);
            }

            bool byId = (toggle?.IsChecked == true);
            filtered.Sort((a,b) =>
            {
                string ka = byId ? GetId(a)   : GetName(a);
                string kb = byId ? GetId(b)   : GetName(b);
                return string.Compare(ka, kb, StringComparison.OrdinalIgnoreCase);
            });

            var selected = lstGames.SelectedItem;
            _isFiltering = true;
            lstGames.BeginInit();
            try
            {
                lstGames.Items.Clear();
                foreach (var pi in filtered)
                    lstGames.Items.Add(pi);
            }
            finally
            {
                lstGames.EndInit();
                _isFiltering = false;
            }

            if (selected != null)
            {
                foreach (var pi in lstGames.Items)
                {
                    if (ReferenceEquals(pi, selected))
                    {
                        lstGames.SelectedItem = pi;
                        lstGames.ScrollIntoView(pi);
                        break;
                    }
                }
            }
        }

        // Parse helpers (Display like: "Name (ID)")
        static string GetName(PatchItem pi)
        {
            var s = pi.Display ?? string.Empty;
            int idx = s.LastIndexOf(" (");
            if (idx > 0) return s.Substring(0, idx).Trim();
            return s.Trim();
        }

        static string GetId(PatchItem pi)
        {
            var s = pi.Display ?? string.Empty;
            int open = s.LastIndexOf('(');
            int close = s.LastIndexOf(')');
            if (open >= 0 && close > open)
                return s.Substring(open + 1, close - open - 1).Trim();
            return string.Empty;
        }

        // Persist sort mode under %LOCALAPPDATA%\ApolloGUI\userprefs.txt
        void LoadSortMode()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApolloGUI");
                var file = Path.Combine(dir, "userprefs.txt");
                if (File.Exists(file))
                {
                    var text = File.ReadAllText(file).Trim();
                    bool byId = string.Equals(text, "Id", StringComparison.OrdinalIgnoreCase);
                    if (SortToggle != null)
                    {
                        SortToggle.IsChecked = byId;
                        SortToggle.Content = byId ? "Sort: ID" : "Sort: Name";
                    }
                }
                else
                {
                    if (SortToggle != null) SortToggle.Content = "Sort: Name";
                }
            }
            catch { }
        }

        void SaveSortMode()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApolloGUI");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "userprefs.txt");
                var mode = (SortToggle?.IsChecked == true) ? "Id" : "Name";
                File.WriteAllText(file, mode);
            }
            catch { }
        }
    }
}
