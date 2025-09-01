// ============================================================================
//  ApolloGUI — ModFillDialog.xaml.cs
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
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ApolloGUI
{
    public partial class ModFillDialog : Window
    {
        public string CodeName { get; set; } = string.Empty;

        private readonly Dictionary<string, ModBlock> _tokenBlocks = new(StringComparer.OrdinalIgnoreCase);
        public enum ReturnMode { FirstColumnValue, FullRowTabbed }
        private readonly ReturnMode _mode;
        public Dictionary<string, string> Result { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ModFillDialog(string codeName, Dictionary<string, ModBlock> tokenBlocks, ReturnMode mode = ReturnMode.FirstColumnValue)
        {
            InitializeComponent();
            CodeName = codeName;
            DataContext = this;
            _mode = mode;

            foreach (var kv in tokenBlocks)
                _tokenBlocks[kv.Key] = kv.Value;

            BuildTabs();
        }

        private void BuildTabs()
        {
            Tabs.Items.Clear();
            foreach (var kv in _tokenBlocks)
            {
                var token = kv.Key;
                var block = kv.Value;

                var box = new ModCodeBox { Block = block, Margin = new Thickness(4) };
                var dg = FindGrid(box);
                if (dg != null) dg.SelectionMode = DataGridSelectionMode.Single;

                var tab = new TabItem { Header = token, Content = box, Tag = token };
                Tabs.Items.Add(tab);
            }
            if (Tabs.Items.Count > 0) Tabs.SelectedIndex = 0;
        }

        private static DataGrid? FindGrid(DependencyObject root)
        {
            if (root is DataGrid dg) return dg;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                var found = FindGrid(child);
                if (found != null) return found;
            }
            return null;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            Result.Clear();

            foreach (TabItem tab in Tabs.Items)
            {
                var token = tab.Tag as string ?? "";
                if (tab.Content is not ModCodeBox box) continue;

                var dg = FindGrid(box);
                if (dg == null) continue;

                if (dg.SelectedItem is string[] row)
                {
                    string value;
                    if (_mode == ReturnMode.FirstColumnValue)
                        value = row.Length > 0 ? row[0] ?? string.Empty : string.Empty;
                    else
                        value = string.Join("\t", row);

                    Result[token] = value;
                }
                else
                {
                    continue;
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
