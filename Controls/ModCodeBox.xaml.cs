// ============================================================================
//  ApolloGUI — ModCodeBox.xaml.cs
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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ApolloGUI
{
    public partial class ModCodeBox : UserControl
    {
        public ModCodeBox()
        {
            InitializeComponent();
            DataContext = this;
        }

        public static readonly DependencyProperty BlockProperty =
            DependencyProperty.Register(nameof(Block), typeof(ModBlock), typeof(ModCodeBox),
                new PropertyMetadata(null, OnBlockChanged));

        public ModBlock Block
        {
            get => (ModBlock)GetValue(BlockProperty);
            set => SetValue(BlockProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            private set => SetValue(TitlePropertyKey, value);
        }
        private static readonly DependencyPropertyKey TitlePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Title), typeof(string), typeof(ModCodeBox),
                new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty TitleProperty = TitlePropertyKey.DependencyProperty;

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            private set => SetValue(SubtitlePropertyKey, value);
        }
        private static readonly DependencyPropertyKey SubtitlePropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(Subtitle), typeof(string), typeof(ModCodeBox),
                new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty SubtitleProperty = SubtitlePropertyKey.DependencyProperty;

        private static void OnBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (ModCodeBox)d;
            view.RebuildGrid();
        }

        private void RebuildGrid()
        {
            Grid.Columns.Clear();
            Grid.ItemsSource = null;

            var b = Block;
            if (b == null) { Title = string.Empty; Subtitle = string.Empty; return; }

            var headers = (b.Headers ?? new List<string>()).ToList();
            if (headers.Count == 1 && (headers[0]?.Contains(">") ?? false))
            {
                headers = headers[0].Split('>').Select(h => (h ?? string.Empty).Trim()).Where(h => h.Length > 0).ToList();
            }

            Title = b.Name ?? string.Empty;
            Subtitle = headers.Count > 0 ? string.Join(" | ", headers) : "—";

            int colCount = Math.Max(headers.Count, 1);
            for (int i = 0; i < colCount; i++)
            {
                var headerText = i < headers.Count ? headers[i] : $"Col {i+1}";
                var col = new DataGridTextColumn
                {
                    Header = headerText,
                    Binding = new Binding($"[{i}]")
                };
                Grid.Columns.Add(col);
            }

            var items = new List<string[]>();
            foreach (var row in b.Rows)
            {
                var arr = new string[Math.Max(colCount, row.Count)];
                for (int i = 0; i < arr.Length; i++) arr[i] = i < row.Count ? row[i] : string.Empty;
                items.Add(arr);
            }
            Grid.ItemsSource = items;
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Grid.ItemsSource is not IEnumerable<string[]> src) return;
            var q = (FilterBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(q))
            {
                Grid.ItemsSource = src.ToList();
                return;
            }
            Grid.ItemsSource = src.Where(r => r.Any(c => c?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        }

        private void CopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem is string[] arr)
            {
                var line = string.Join("\t", arr);
                try { Clipboard.SetText(line); } catch { }
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.ItemsSource is not IEnumerable<string[]> src) return;
            var sb = new StringBuilder();
            var headers = (Block?.Headers ?? new List<string>()).ToList();
            if (headers.Count == 1 && (headers[0]?.Contains(">") ?? false))
                headers = headers[0].Split('>').Select(h => (h ?? string.Empty).Trim()).Where(h => h.Length > 0).ToList();

            if (headers.Count > 0)
            {
                sb.AppendLine(string.Join(",", headers.Select(h => Quote(h))));
            }
            foreach (var row in src)
            {
                sb.AppendLine(string.Join(",", row.Select(Quote)));
            }

            var fileName = $"{(Block?.Name ?? "MOD")}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            System.IO.File.WriteAllText(temp, sb.ToString(), Encoding.UTF8);
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo { FileName = temp, UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        private static string Quote(string? s)
        {
            if (s == null) return string.Empty;
            if (s.Contains(',') || s.Contains('"') || s.Contains('\t'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
