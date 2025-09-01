// CollectorWindow.xaml.cs — SW export/preview: line-preserving + always padded
// - Uses SwFormatter via reflection if present (e.g., SwFormatter.NormalizeSwBlocksForCollector)
// - Otherwise falls back to a robust line-preserving formatter that ALWAYS pads to pairs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApolloGUI
{
    public class CollectorEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        private string _code = string.Empty;
        public string Code
        {
            get => _code;
            set
            {
                if (_code != value)
                {
                    _code = value ?? string.Empty;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
                }
            }
        }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public override string ToString() => Name;
    }

    public partial class CollectorWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly MainWindow _owner;
        private bool _allowClose = false;
        private double _baselineWidth;
        private bool _loaded;

        private const double LeftPaneWidth = 420;
        private const double PreviewPaneMinWidth = 560;
        private const double SplitterWidth = 5;
        private const double ChromePad = 24;

        public ObservableCollection<CollectorEntry> Entries { get; } = new();

        public CollectorWindow(MainWindow owner)
        {
            InitializeComponent();
            _owner = owner;
            DataContext = this;
            Loaded += (_, __) =>
            {
                if (!_loaded)
                {
                    _baselineWidth = this.Width;
                    _loaded = true;
                    UpdatePreviewVisibility();
                    UpdateCodePreviewFromSelection();
                }
            };
        }

        public void AllowRealClose() => _allowClose = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }
            base.OnClosing(e);
        }

        private bool _seeCodesEnabled;
        public bool SeeCodesEnabled
        {
            get => _seeCodesEnabled;
            set
            {
                if (_seeCodesEnabled != value)
                {
                    _seeCodesEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeeCodesEnabled)));
                    UpdatePreviewVisibility();
                    UpdateCodePreviewFromSelection();
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C &&
                (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SeeCodesEnabled = !SeeCodesEnabled;
                e.Handled = true;
            }
        }

        private void UpdatePreviewVisibility()
        {
            try
            {
                var container = this.FindName("CodePreviewContainer") as FrameworkElement;
                if (container != null)
                    container.Visibility = SeeCodesEnabled ? Visibility.Visible : Visibility.Collapsed;

                if (_loaded)
                {
                    if (SeeCodesEnabled)
                        this.Width = Math.Max(_baselineWidth, LeftPaneWidth + SplitterWidth + PreviewPaneMinWidth + ChromePad);
                    else
                        this.Width = _baselineWidth;
                }
            }
            catch { }
        }

        private void List_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateCodePreviewFromSelection();
        }

        // Note: double-click handler lives in a separate partial (CollectorWindow.Events.cs)

        private void UpdateCodePreviewFromSelection()
        {
            try
            {
                var tb = this.FindName("CodePreviewText") as TextBox;
                if (tb == null) return;

                var list = this.FindName("List") as ListBox;
                var sel = list?.SelectedItem as CollectorEntry
                          ?? Entries.FirstOrDefault(e => e.IsChecked)
                          ?? Entries.FirstOrDefault();

                var text = sel?.Code ?? string.Empty;
                tb.Text = NormalizeSw(text, preserveLines: true, padPairs: true);
            }
            catch { }
        }

        private static readonly Regex UnresolvedTokenRegex =
            new Regex(@"\{(Amount:[^}]+|[A-Za-z0-9_]+)\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static bool TextHasUnresolvedTokens(string? codeText, out string details)
        {
            details = string.Empty;
            if (string.IsNullOrEmpty(codeText)) return false;
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in UnresolvedTokenRegex.Matches(codeText))
            {
                if (!m.Success) continue;
                var tok = m.Groups[1].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(tok)) found.Add(tok);
            }
            if (found.Count > 0)
            {
                details = string.Join(", ", found);
                return true;
            }
            return false;
        }

private async void Patch_Checked_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var checkedItems = Entries.Where(x => x.IsChecked).ToList();
        if (checkedItems.Count == 0)
        {
            MessageBox.Show(this, "No items are checkmarked.", "Patch Checkmarked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await BuildAndPatchAsync(checkedItems);
    }
    catch (Exception ex)
    {
        MessageBox.Show(this, ex.Message, "Patch Checkmarked", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

private async void ApplyAll_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var allItems = Entries.ToList();   // <— ignore IsChecked
        if (allItems.Count == 0)
        {
            MessageBox.Show(this, "The Collector is empty.", "Apply All", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await BuildAndPatchAsync(allItems);
    }
    catch (Exception ex)
    {
        MessageBox.Show(this, ex.Message, "Apply All", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

        private async System.Threading.Tasks.Task BuildAndPatchAsync(List<CollectorEntry> blocks)
        {
            // Compose a .savepatch in temp with SW-style normalized code, preserving original line boundaries and padding pairs
            var sb = new StringBuilder();
            foreach (var it in blocks)
            {
                sb.AppendLine($"[{it.Name}]");

                var normalized = NormalizeSw(it.Code ?? string.Empty, preserveLines: true, padPairs: true);
                foreach (var line in normalized.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
                    sb.AppendLine(line);
            }

            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "collector.temp.savepatch");
            System.IO.File.WriteAllText(tmp, sb.ToString());

            try { _ = _owner.RunCollectorPatchAsync(tmp, blocks.Count); }
            catch { }

            await System.Threading.Tasks.Task.CompletedTask;
        }

        private void Clear_Click(object sender, RoutedEventArgs e) => ClearAll();

        public void ClearAll()
        {
            Entries.Clear();
            var tb = this.FindName("CodePreviewText") as TextBox;
            if (tb != null) tb.Clear();
        }

        public void AddOrUpdate(string name, string code)
        {
            if (TextHasUnresolvedTokens(code, out var _missing))
            {
                MessageBox.Show(this,
                    "This code still has unresolved tokens.\nPlease fill MOD values before adding to the Collector.",
                    "Collector", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(name)) return;
            var existing = Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Code = code ?? string.Empty;
                existing.IsChecked = true;
            }
            else
            {
                Entries.Add(new CollectorEntry { Name = name, Code = code ?? string.Empty, IsChecked = true });
            }

            UpdateCodePreviewFromSelection();
        }

        // CENTRAL NORMALIZER: Try SwFormatter via reflection; fallback to our own line-preserving padded normalizer.
        private static string NormalizeSw(string text, bool preserveLines, bool padPairs)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Try ApolloGUI.Utils.SwFormatter or SwFormatter.*
            try
            {
                var type = Type.GetType("ApolloGUI.Utils.SwFormatter")
                           ?? Type.GetType("SwFormatter");
                if (type != null)
                {
                    // Prefer NormalizeSwBlocksForCollector if present
                    foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        if (m.Name == "NormalizeSwBlocksForCollector" || m.Name == "NormalizeSwBlocks")
                        {
                            var ps = m.GetParameters();
                            object? result = null;
                            if (ps.Length == 1 && ps[0].ParameterType == typeof(string))
                            {
                                result = m.Invoke(null, new object?[] { text });
                            }
                            else if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(bool))
                            {
                                // Assume 2nd parameter is "preserveLines" or "pad", send preserveLines
                                result = m.Invoke(null, new object?[] { text, preserveLines });
                            }
                            else if (ps.Length == 3 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(bool) && ps[2].ParameterType == typeof(bool))
                            {
                                // (text, preserveLines, padPairs)
                                result = m.Invoke(null, new object?[] { text, preserveLines, padPairs });
                            }

                            if (result is string s && !string.IsNullOrWhiteSpace(s))
                                return s;
                        }
                    }
                }
            }
            catch
            {
                // fall through to local
            }

            return NormalizeSw_Local(text, preserveLines, padPairs);
        }

        // Fallback: line-preserving SW formatter that ALWAYS pads to pairs
        private static string NormalizeSw_Local(string text, bool preserveLines, bool padPairs)
        {
            var hex8 = new System.Text.RegularExpressions.Regex(@"\b([0-9A-Fa-f]{8})\b");
            var inputLines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var outSb = new StringBuilder();

            foreach (var raw in inputLines)
            {
                var ms = hex8.Matches(raw);
                if (ms.Count == 0)
                {
                    if (preserveLines)
                        outSb.AppendLine(); // keep blank line
                    continue;
                }

                for (int i = 0; i < ms.Count; i += 2)
                {
                    var a = ms[i].Groups[1].Value.ToUpperInvariant();
                    if (i + 1 < ms.Count)
                    {
                        var b = ms[i + 1].Groups[1].Value.ToUpperInvariant();
                        outSb.AppendLine($"{a} {b}");
                    }
                    else
                    {
                        // Odd token: pad with zeros if requested
                        outSb.AppendLine(padPairs ? $"{a} 00000000" : a);
                    }
                }
            }

            return outSb.ToString()
                        .Replace("\r\n", "\n").Replace("\r", "\n")
                        .Replace("\n", Environment.NewLine)
                        .TrimEnd();
        }
private void ListItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    try
    {
        if (sender is ListBoxItem lbi)
        {
            lbi.IsSelected = true;
            UpdateCodePreviewFromSelection();
        }
    }
    catch { }
}
}
}