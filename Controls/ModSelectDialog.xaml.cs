// ============================================================================
//  ApolloGUI — ModSelectDialog.xaml.cs
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
using System.Windows.Data;

namespace ApolloGUI
{
    public partial class ModSelectDialog : Window
    {
        public string TagName { get; }
        public string Caption { get; }
        public string? SelectedValue { get; private set; }
        public string? SelectedName { get; private set; }

        private readonly List<string> _headers;
        private readonly List<string[]> _rows;

        public ModSelectDialog(string tagName, List<string> headers, List<List<string>> rows, string caption)
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.WidthAndHeight;
            this.MaxHeight = 720;
            this.MinWidth = 420;

            TagName = tagName ?? string.Empty;
            Caption = caption ?? "Select MOD";

            _headers = (headers ?? new List<string>()).ToList();
            _rows = new List<string[]>();

            // If one header like "Value>Name>Type", split defensively at render-time
            if (_headers.Count == 1 && (_headers[0]?.Contains(">") ?? false))
                _headers = _headers[0].Split('>').Select(h => (h ?? string.Empty).Trim()).Where(h => h.Length > 0).ToList();

            foreach (var r in rows ?? new List<List<string>>())
            {
                var arr = new string[Math.Max(_headers.Count, r.Count)];
                for (int i = 0; i < arr.Length; i++) arr[i] = i < r.Count ? r[i] ?? string.Empty : string.Empty;
                _rows.Add(arr);
            }

            BuildView();
            DataContext = this;
        }

        private void BuildView()
        {
            int headerCols = _headers.Count;
            int rowCols = _rows.Any() ? _rows.Max(r => r?.Length ?? 0) : 0;
            int colCount = Math.Max(headerCols, rowCols);

            bool useGrid = colCount >= 2; // table when 2+ columns

            if (!useGrid)
            {
                List.Visibility = Visibility.Visible;
                Grid.Visibility = Visibility.Collapsed;
                List.Items.Clear();
                foreach (var r in _rows)
                {
                    string value = r.Length > 0 ? r[0] : string.Empty;
                    string name = r.Length > 1 ? r[1] : string.Empty;
                    List.Items.Add(string.IsNullOrWhiteSpace(name) ? value : $"{value} = {name}");
                }
            }
            else
            {
                Grid.Visibility = Visibility.Visible;
                List.Visibility = Visibility.Collapsed;
                Grid.Columns.Clear();

                var headersToUse = _headers.ToList();
                if (headersToUse.Count < colCount)
                {
                    for (int i = headersToUse.Count; i < colCount; i++)
                        headersToUse.Add($"Col {i + 1}");
                }

                for (int i = 0; i < headersToUse.Count; i++)
                {
                    Grid.Columns.Add(new DataGridTextColumn
                    {
                        Header = headersToUse[i],
                        Binding = new Binding($"[{i}]"),
                        Width = DataGridLength.SizeToCells
                    });
                }
                Grid.ItemsSource = _rows;
                Grid.UpdateLayout();
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var q = (FilterBox.Text ?? string.Empty).Trim();

            int headerCols = _headers.Count;
            int rowCols = _rows.Any() ? _rows.Max(r => r?.Length ?? 0) : 0;
            int colCount = Math.Max(headerCols, rowCols);
            bool useGrid = colCount >= 2;

            if (!useGrid)
            {
                if (string.IsNullOrEmpty(q)) { BuildView(); return; }
                List.Items.Clear();
                foreach (var r in _rows)
                {
                    if (r.Any(c => (c ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        string value = r.Length > 0 ? r[0] : string.Empty;
                        string name = r.Length > 1 ? r[1] : string.Empty;
                        List.Items.Add(string.IsNullOrWhiteSpace(name) ? value : $"{value} = {name}");
                    }
                }
            }
            else
            {
                if (string.IsNullOrEmpty(q)) { Grid.ItemsSource = _rows.ToList(); Grid.UpdateLayout(); return; }
                Grid.ItemsSource = _rows.Where(r => r.Any(c => (c ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                Grid.UpdateLayout();
            }
        }

        private void Accept_OnDoubleClick(object sender, RoutedEventArgs e) => OK_Click(sender, e);

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (_headers.Count <= 1)
            {
                int idx = List.SelectedIndex;
                if (idx < 0 || idx >= _rows.Count) { DialogResult = false; return; }
                var r = _rows[idx];
                SelectedValue = r.Length > 0 ? r[0] : string.Empty;
                SelectedName = r.Length > 1 ? r[1] : null;
            }
            else
            {
                if (Grid.SelectedItem is not string[] r) { DialogResult = false; return; }
                SelectedValue = r.Length > 0 ? r[0] : string.Empty;
                SelectedName = r.Length > 1 ? r[1] : null;
            }
            DialogResult = true;
            Close();
        }
    }
}
