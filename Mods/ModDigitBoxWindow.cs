// ============================================================================
//  ApolloGUI — ModDigitBoxWindow.cs
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
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ApolloGUI
{
    public sealed class ModDigitBoxWindow : Window
    {
        private readonly TabControl _tabs = new() { Margin = new Thickness(12) };
        private TextBox? _search;
        private List<ModBlock> _allBlocks = new();

        private ModDigitBoxWindow()
        {
            Title = "Mod Digit Box";
            Width = 860;
            Height = 560;
            Content = BuildLayout();
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private UIElement BuildLayout()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12,12,12,4) };
            var lbl = new TextBlock { Text = "Filter:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) };
            _search = new TextBox { Width = 260 };
            _search.TextChanged += (_, __) => ApplyFilter();
            sp.Children.Add(lbl);
            sp.Children.Add(_search);

            Grid.SetRow(sp, 0);
            Grid.SetRow(_tabs, 1);
            grid.Children.Add(sp);
            grid.Children.Add(_tabs);
            return grid;
        }

        public static void ShowForCode(string codeName, string fileText, string? referenceLine = null)
        {
            var blocks = ModBlockParser.ParseAll(fileText);
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(referenceLine))
            {
                foreach (Match m in Regex.Matches(referenceLine, @"\{(?<n>[A-Za-z0-9_]+)\}"))
                {
                    var key = m.Groups["n"].Value;
                    if (blocks.ContainsKey(key)) names.Add(key);
                }
            }

            List<ModBlock> toShow;
            if (names.Count > 0)
                toShow = new List<ModBlock> { blocks[names[0]] };
            else if (blocks.Count > 0)
                toShow = new List<ModBlock> { blocks.Values.First() };
            else
                toShow = new List<ModBlock>();

            var wnd = new ModDigitBoxWindow { Title = $"Mod Digit Box — {codeName}" };
            wnd._allBlocks = toShow;
            wnd.BuildTabs(wnd._allBlocks);

            try { wnd.Owner = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault(w => w.IsActive); } catch { }
            wnd.Show();
        }

        public static void ShowForCodeAllTokens(string codeName, string fileText, string? referenceLine = null)
        {
            var blocks = ModBlockParser.ParseAll(fileText);
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(referenceLine))
            {
                foreach (Match m in Regex.Matches(referenceLine, @"\{(?<n>[A-Za-z0-9_]+)\}"))
                {
                    var key = m.Groups["n"].Value;
                    if (blocks.ContainsKey(key)) names.Add(key);
                }
            }
            if (names.Count == 0) names = blocks.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

            var wnd = new ModDigitBoxWindow { Title = $"Mod Digit Box — {codeName}" };
            wnd._allBlocks = names.Select(n => blocks[n]).ToList();
            wnd.BuildTabs(wnd._allBlocks);

            try { wnd.Owner = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault(w => w.IsActive); } catch { }
            wnd.Show();
        }

        private void BuildTabs(List<ModBlock> blocks)
        {
            _tabs.Items.Clear();
            if (blocks.Count == 0)
            {
                _tabs.Items.Add(new TabItem
                {
                    Header = "No MOD blocks",
                    Content = new TextBlock { Text = "No {NAME} blocks found in this file.", Margin = new Thickness(12) }
                });
                return;
            }

            foreach (var b in blocks)
            {
                var box = new ModCodeBox { Block = b, Margin = new Thickness(4) };
                var tab = new TabItem { Header = b.Name, Content = box };
                _tabs.Items.Add(tab);
            }
        }

        private void ApplyFilter()
        {
            string q = _search?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(q))
            {
                BuildTabs(_allBlocks);
                return;
            }
            q = q.Trim();
            var filtered = _allBlocks.Where(b =>
                (b.Name?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                b.Headers.Any(h => h?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                b.Rows.Any(r => r.Any(c => c?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
            ).ToList();

            BuildTabs(filtered);
        }
    }
}
