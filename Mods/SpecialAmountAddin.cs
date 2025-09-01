// ============================================================================
//  ApolloGUI — SpecialAmountAddin.cs
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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ApolloGUI
{
    /// <summary>
    /// Appends "Fill Special AMOUNT…" to the existing MOD context menu for blocks
    /// that contain {AMOUNT[:DEFAULT[:TYPE[:ENDIAN]]]} tokens.
    /// </summary>
    public static class SpecialAmountAddin
    {

            private static byte[] BuildBytesForValue(ulong value, int widthBytes)
            {
                if (widthBytes < 1) widthBytes = 1;
                if (widthBytes > 8) widthBytes = 8;
                var all = BitConverter.GetBytes(value);
                var slice = new byte[widthBytes];
                Array.Copy(all, 0, slice, 0, widthBytes);
                return slice;
            }
        


            private static string BuildHexFromValue(ulong value, int widthBytes, bool bigEndian)
            {
                if (widthBytes < 1) widthBytes = 1;
                if (widthBytes > 8) widthBytes = 8;
                var all = BitConverter.GetBytes(value);
                var slice = new byte[widthBytes];
                Array.Copy(all, 0, slice, 0, widthBytes);
                return string.Concat(slice.Select(b => b.ToString("X2")));
            }
        


        private static byte[] GetMinimalBytesForAmount(ulong n, int maxBytes = 4)
        {
            int width;
            if (n <= 0xFFUL) width = 1;
            else if (n <= 0xFFFFUL) width = 2;
            else if (n <= 0xFFFFFFFFUL) width = 4;
            else width = 8;
            if (maxBytes < width) width = maxBytes;
            if (width < 1) width = 1;
            var all = BitConverter.GetBytes(n);
            var outBytes = new byte[width];
            Array.Copy(all, 0, outBytes, 0, width);
            return outBytes;
        }
    

        private static readonly Regex AmountToken = new(@"\{AMOUNT(?::(?<def>(?:NA|[0-9A-Fa-f]*))(?::(?<type>HEX|FLOAT|ABC123|UTF08|UTF16))?(?::(?<endian>BIG|LITTLE|TXT))?)?\}",
                                                        RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void AttachToCodesPanel(ItemsControl codesPanel)
        {
            if (codesPanel == null) return;

            codesPanel.ContextMenuOpening += (s, e) =>
            {
                // Defer to let the base integration build its menu first
                codesPanel.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    try
                    {
                        var src = e.OriginalSource as DependencyObject;
                        var container = ItemsControl.ContainerFromElement(codesPanel, src);
                        var item = (container as ContentPresenter)?.Content
                                   ?? (container as ListBoxItem)?.DataContext
                                   ?? (container as ListViewItem)?.DataContext;
                        if (item == null) return;

                        var t = item.GetType();
                        var linesProp = t.GetProperty("Lines");
                        var linesObj = linesProp?.GetValue(item) as System.Collections.IList;
                        if (linesObj == null) return;

                        var lines = new List<string>();
                        foreach (var o in linesObj) lines.Add(o?.ToString() ?? string.Empty);

                        int idx = FindFirstAmountLineIndex(lines);
                        if (idx < 0) return;

                        var menu = codesPanel.ContextMenu ?? new ContextMenu();

                        // Remove any prior duplicate item
                        foreach (var mi in menu.Items.OfType<MenuItem>().ToList())
                        {
                            if ((mi.Header as string)?.Contains("Fill Special AMOUNT", StringComparison.OrdinalIgnoreCase) == true)
                                menu.Items.Remove(mi);
                        }

                        var m = AmountToken.Match(lines[idx] ?? string.Empty);
                        string def = "00000000", typeS = "HEX", endian = "BIG";
                        if (m.Success)
                        {
                            if (m.Groups["def"].Success && !string.IsNullOrEmpty(m.Groups["def"].Value)) def = m.Groups["def"].Value;
                            if (m.Groups["type"].Success && !string.IsNullOrEmpty(m.Groups["type"].Value)) typeS = m.Groups["type"].Value.ToUpperInvariant();
                            if (m.Groups["endian"].Success && !string.IsNullOrEmpty(m.Groups["endian"].Value)) endian = m.Groups["endian"].Value.ToUpperInvariant();
                        }

                        if (menu.Items.OfType<MenuItem>().Any(mi => {
                            var h = (mi.Header as string) ?? string.Empty;
                            return string.Equals(h, "Open MOD Values…", StringComparison.OrdinalIgnoreCase)
                                   || h.IndexOf("Fill MODs", StringComparison.OrdinalIgnoreCase) >= 0; }))
                        { codesPanel.ContextMenu = menu; return; }
                        var amountItem = new MenuItem { Header = "Fill Special AMOUNT…" };
                        amountItem.Click += (_, __) =>
                        {
                            try
                            {
                                string initialDisplay = 
    (string.Equals(typeS, "ABC123", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(typeS, "UTF08", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(typeS, "UTF16", StringComparison.OrdinalIgnoreCase))
        ? string.Empty
        : def;
                                if (string.Equals(typeS, "FLOAT", StringComparison.OrdinalIgnoreCase) && TryHexToBytes(def, out var dbytes))
                                {
                                    if (string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase)) Array.Reverse(dbytes);
                                    float f = BitConverter.ToSingle(dbytes, 0);
                                    initialDisplay = f.ToString(CultureInfo.InvariantCulture);
                                }
                // Inline limit for text modes: maxLen from default length; NA => unlimited
                int? maxLen = null;
                var __defTrim = def?.Trim();
                if (!string.IsNullOrEmpty(__defTrim) && !string.Equals(__defTrim, "NA", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(typeS, "ABC123", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(typeS, "UTF08", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(typeS, "UTF16", StringComparison.OrdinalIgnoreCase))
                    {
                        maxLen = __defTrim.Length;
                    }
                    else if (int.TryParse(__defTrim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var __n) && __n > 0)
                    {
                        maxLen = __n;
                    }
                }
        

                                var dlg = new AmountPromptWindow(typeS, endian, initialDisplay, def, allowTypeAndEndianSelection: !(string.Equals(typeS, "ABC123", StringComparison.OrdinalIgnoreCase) && string.Equals(endian, "TXT", StringComparison.OrdinalIgnoreCase)) && !string.Equals(typeS, "UTF08", StringComparison.OrdinalIgnoreCase) && !string.Equals(typeS, "UTF16", StringComparison.OrdinalIgnoreCase))
                                { Owner = Window.GetWindow(codesPanel) };

                                if (dlg.ShowDialog() == true)
                                {
                                    
            int? _maxLen = null;
            if (!string.IsNullOrWhiteSpace(def) && !string.Equals(def, "NA", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(def, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var _n) && _n > 0)
                    _maxLen = _n;
            }
            if (_maxLen.HasValue && (string.Equals(typeS, "ABC123", StringComparison.OrdinalIgnoreCase) || string.Equals(typeS, "UTF08", StringComparison.OrdinalIgnoreCase) || string.Equals(typeS, "UTF16", StringComparison.OrdinalIgnoreCase)))
            {
                var _slen = (dlg.ResultValue ?? string.Empty).Length;
                if (_slen > _maxLen.Value)
                {
                    MessageBox.Show(Window.GetWindow(codesPanel), $"Max length is {_maxLen.Value} characters for this MOD.", "Special MOD", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
    string chosenType = 
    (!string.Equals(typeS, "HEX", StringComparison.OrdinalIgnoreCase) && !string.Equals(typeS, "FLOAT", StringComparison.OrdinalIgnoreCase))
        ? typeS
        : (dlg.SelectedType?.ToUpperInvariant() ?? typeS);
string chosenEndian = 
    (!string.Equals(typeS, "HEX", StringComparison.OrdinalIgnoreCase) && !string.Equals(typeS, "FLOAT", StringComparison.OrdinalIgnoreCase))
        ? endian
        : (dlg.SelectedEndian?.ToUpperInvariant() ?? endian);
                                    string resultHex = ComputeHex(dlg.ResultValue, chosenType, chosenEndian);
                                    if (string.IsNullOrEmpty(resultHex)) return;

                                    string line = lines[idx] ?? string.Empty;
                                    string newLine = AmountToken.Replace(line, resultHex, 1);

                                    // Write back via reflection
                                    var list = linesProp?.GetValue(item) as System.Collections.IList;
                                    if (list != null && idx >= 0 && idx < list.Count)
                                    {
                                        list[idx] = newLine;
                                    }

                                    if (codesPanel is ListBox lb) lb.Items.Refresh();
                                    else if (codesPanel is ListView lv) lv.Items.Refresh();

                                    Mouse.Capture(null);
                                    codesPanel.Focus();
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, "Fill Special AMOUNT", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        };

                        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
                        menu.Items.Add(amountItem);
                        codesPanel.ContextMenu = menu;
                    }
                    catch { /* non-fatal */ }
                }));
            };
        }

        private static int FindFirstAmountLineIndex(IList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (AmountToken.IsMatch(lines[i] ?? string.Empty)) return i;
            }
            return -1;
        }

        private static bool TryHexToBytes(string hex, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            try
            {
                hex = (hex ?? "").Replace("_", "").Trim();
                if (hex == "") { bytes = new byte[4]; return true; }
                if ((hex.Length % 2) == 1) hex = "0" + hex;
                var raw = Enumerable.Range(0, hex.Length / 2)
                    .Select(i => byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber))
                    .ToArray();
                if (raw.Length >= 4) bytes = raw.TakeLast(4).ToArray();
                else bytes = Enumerable.Repeat((byte)0, 4 - raw.Length).Concat(raw).ToArray();
                return true;
            }
            catch { return false; }
        }

        private static string ComputeHex(string input, string type, string endian)
        
        {
            try
            {
                byte[] bytes;

                if (string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) && string.Equals(endian, "TXT", StringComparison.OrdinalIgnoreCase))
{
    return input ?? string.Empty;
}

                else if (string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase))
                {
                    bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
                    if (string.Equals(endian, "LITTLE", StringComparison.OrdinalIgnoreCase))
                        System.Array.Reverse(bytes);
                }
                else if (string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase))
                {
                    var enc = string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase)
                        ? System.Text.Encoding.BigEndianUnicode
                        : System.Text.Encoding.Unicode;
                    bytes = enc.GetBytes(input ?? string.Empty);
                }
                else if (string.Equals(type, "HEX", StringComparison.OrdinalIgnoreCase))
                {
                    string hex = (input ?? "").Trim().TrimStart('0', 'x', 'X');
                    if (hex == "") hex = "0";
                    if ((hex.Length % 2) == 1) hex = "0" + hex;
                    var raw = Enumerable.Range(0, hex.Length / 2)
                        .Select(i => byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber))
                        .ToArray();
                    if (raw.Length >= 4) bytes = raw.TakeLast(4).ToArray();
                    else bytes = Enumerable.Repeat((byte)0, 4 - raw.Length).Concat(raw).ToArray();
                    if (string.Equals(endian, "LITTLE", StringComparison.OrdinalIgnoreCase))
                        System.Array.Reverse(bytes);
                }
                else if (string.Equals(type, "DEC", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ulong.TryParse(input ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                        return string.Empty;
                    // Default to 4-byte field here; token seed width is applied in the token-level handler.
                    int widthBytes = 4;
                    bool big = string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase);
                    bytes = BuildBytesForValue(n, widthBytes);
                }

                else // FLOAT (and fallback)
                {
                    if (!float.TryParse(input ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        return string.Empty;
                    bytes = BitConverter.GetBytes(f);
                    if (string.Equals(endian, "LITTLE", StringComparison.OrdinalIgnoreCase))
                        System.Array.Reverse(bytes);
                }

                return string.Concat(bytes.Select(b => b.ToString("X2")));
            }
            catch { return string.Empty; }
        }
    
    }
}
