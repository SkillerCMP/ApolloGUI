// ============================================================================
//  ApolloGUI — ModCodeWindowIntegration.cs
//  Purpose: Parses and renders [MOD] blocks and [Amount:] prompts; ties into editor pane.
//  Key types: Regex parsers; helper methods; UI update hooks.
//  Notes: Supports Unity Count, float/hex modes, endian swap, multi-mod prompts.
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
using System.Windows.Media;
using System.Windows.Data;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;

namespace ApolloGUI
{
    /// <summary>
    /// MOD integration:
    /// - Context menu (Fill/Reset) appears ONLY for items whose Name starts with "-M-" (and not MODS:).
    /// - Uses a snapshot of the ORIGINAL file text captured on first attach for reliable restore/fill.
    /// - If snapshot is null/empty, we automatically fall back to the current full file text (bugfix).
    /// - Restore prefers a section whose code-only lines actually contain {MOD} tokens.
    /// - View-level filter hides the [MODS:] header regardless of ItemsSource rebinding.
    /// - Reset/Fill never bleed into {MOD} block bodies.
    /// </summary>
    public static class ModCodeWindowIntegration
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
        


        // Emits minimal-width little-endian bytes for the given value, clamped between 1..4 bytes.
        // Use BIG endian by reversing the returned array at the call site if needed.
        private static byte[] GetMinimalBytesForAmount(ulong n, int maxBytes = 4)
        {
            int width;
            if (n <= 0xFFUL) width = 1;
            else if (n <= 0xFFFFUL) width = 2;
            else if (n <= 0xFFFFFFFFUL) width = 4;
            else width = 8;

            if (maxBytes < width) width = maxBytes;
            if (width < 1) width = 1;

            var all = BitConverter.GetBytes(n); // little-endian on Windows/.NET
            var outBytes = new byte[width];
            Array.Copy(all, 0, outBytes, 0, width);
            return outBytes;
        }
    

// ----- LINT NOTES (2025-08-31) -----------------------------------------------
// Parsing order is important:
// 1) Expand/resolve [MOD] & {MOD}/{\MOD} blocks *before* injecting [Amount:] values.
//    This ensures Save Wizard code lines that initially include only the first 8 hex
//    digits (prefix) are completed after MOD specializations are applied.
// 2) {Amount:...} should be processed after MOD expansion so that calculator prompts
//    (HEX/FLOAT/BIG/LITTLE) receive the final, context-aware state.
// Unity Count (varint-style) handling:
// - When mode == UNITY or similar, convert Dec <-> UnityCount using your helper.
// - Always respect endian toggles from UI when writing back results.
// Common pitfalls:
// - Regexes that match [MODS:] blocks must allow nested {MODx} sections.
// - Ensure multi-mod prompts keep stable ordering; prefer deterministic keys.
// - Guard against newline-in-constant errors by using verbatim strings or StringBuilder.
// - If code preview "blocks" 64-bit values (00000000 00000000), make sure this occurs
//   only at the *final* rendering stage so internal calculations use raw/unblocked values.
// ------------------------------------------------------------------------------

        private static readonly Dictionary<int, List<string>> s_OriginalTokenizedByIndex = new();

        private static readonly Regex TokenRegex = new(@"\{(?<n>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);
        static readonly Regex AmountTokenRegex =
            new(@"\{AMOUNT(?::(?<def>(?:NA|[0-9A-Fa-f]*))(?::(?<type>HEX|FLOAT|ABC123|UTF08|UTF16))?(?::(?<endian>BIG|LITTLE|TXT))?)?\}",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ModHeaderRegex = new(@"^\{[A-Za-z0-9_]+\}\s*$", RegexOptions.Compiled);
        private static readonly Regex CodeLineRegex = new(@"^[0-9A-Fa-f]{6,}\s", RegexOptions.Compiled);
        private static readonly Regex HeaderLineRegex = new(@"^\[(?<h>[^\]]+)\]\s*$", RegexOptions.Compiled);

        // Snapshot of the original (tokenized) file text captured the first time we attach.
        private static string? _originalFileTextSnapshot;

        public static void AttachToCodesPanel(
            ItemsControl codesPanel,
            Func<object, SavepatchText.CodeBlock?> asBlock,
            Func<string> getFullFileText,
            Action<SavepatchText.CodeBlock, string>? replaceFirstTokenLine = null,
            Action<SavepatchText.CodeBlock, string?>? appendAppliedName = null)
        {
            if (codesPanel == null) return;

            // Capture original text once per file/session (only if non-empty).
            TrySnapshotOriginal(getFullFileText);

            // Hide [MODS:] via view-level filter
            InstallItemsSourceFilter(codesPanel);

            codesPanel.ContextMenuOpening += (s, e) =>
            {
                try
                {
                    if (e.OriginalSource is not DependencyObject origin) { codesPanel.ContextMenu = null; return; }
                    var cb = FindDataContext<SavepatchText.CodeBlock>(origin) ?? asBlock(origin);
                    if (cb == null || !IsModEntry(cb)) { codesPanel.ContextMenu = null; return; }

                    string codeName = cb.Name ?? string.Empty;
                    // BUGFIX: fall back to current file text if snapshot is null/empty
                    string fileText = !string.IsNullOrWhiteSpace(_originalFileTextSnapshot)
                        ? _originalFileTextSnapshot!
                        : SafeGet(getFullFileText);

                    var menu = new ContextMenu();

                    var fillAll = new MenuItem { Header = "Open MOD Values…" };
                    fillAll.Click += (_, __) => 
{
    try
    {
        BackupOriginal(cb);

        var _lines = cb.Lines ?? new List<string>();
        bool any = _lines.Any(l => TokenRegex.IsMatch(l) || AmountTokenRegex.IsMatch(l));
        if (!any)
        {
            // Try backup by index first
            if (s_OriginalTokenizedByIndex.TryGetValue(cb.Index, out var bak) && bak != null && bak.Count > 0)
            {
                cb.Lines = new List<string>(bak);
                ForceRefreshItem(codesPanel, cb);
            }
            else
            {
                // Fallback to header search with cleaned name
                var cleaned = CleanHeaderName(cb.Name);
                var original = ExtractOriginalCodeLines_ByHeaderNamePreferTokenized(fileText, cleaned, cb.Index);
                if (original.Count == 0 && !string.Equals(cleaned, (cb.Name ?? string.Empty), StringComparison.Ordinal))
                {
                    // also try raw name
                    original = ExtractOriginalCodeLines_ByHeaderNamePreferTokenized(fileText, cb.Name ?? string.Empty, cb.Index);
                }
                if (original.Count == 0)
                {
                    MessageBox.Show("No Place to Add Mods, Please Try Another Code",
                                    "Open MOD Values", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var yn = MessageBox.Show("This code currently has no {MOD}/{AMOUNT} tokens.\n\nRestore the original lines with tokens and continue?",
                                         "Open MOD Values", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (yn != MessageBoxResult.Yes) return;

                cb.Lines = original;
                cb.Lines = new List<string>(cb.Lines);
                ForceRefreshItem(codesPanel, cb);
                // Now that we have tokens again, save a backup
                BackupOriginal(cb);
            }
        }

        RunOpenModValuesFlowUnified(codesPanel, cb, codeName, fileText, replaceFirstTokenLine, appendAppliedName);
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message, "Open MOD Values", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
;
;
                    menu.Items.Add(fillAll);

                    menu.Items.Add(new Separator());

                    var reset = new MenuItem { Header = "Reset this code (restore {MOD} tokens)" };
                    reset.Click += (_, __) =>
                    {
                        try
                        {
                            var original = ExtractOriginalCodeLines_ByHeaderNamePreferTokenized(fileText, CleanHeaderName(cb.Name), cb.Index);
                            if (original.Count == 0)
                            {
                                MessageBox.Show("Original code section (by header name) was not found.", "Reset",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }
                            cb.Lines = original;
                            cb.Lines = new List<string>(cb.Lines); // rebind
                            ForceRefreshItem(codesPanel, cb);
                            replaceFirstTokenLine?.Invoke(cb, cb.Lines[0]);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Reset", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    };
                    menu.Items.Add(reset);

                    var copy = new MenuItem { Header = "Copy" };
copy.Click += (_, __) =>
{
    try
    {
        var sb = new System.Text.StringBuilder();
        if (cb?.Lines != null)
        {
            foreach (var line in cb.Lines) sb.AppendLine(line);
        }
        else if (!string.IsNullOrEmpty(codeName))
        {
            sb.AppendLine(codeName);
        }
        var txt = sb.ToString().TrimEnd();
        if (!string.IsNullOrEmpty(txt))
            Clipboard.SetText(txt);
    }
    catch { }
};
menu.Items.Add(copy);

codesPanel.ContextMenu = menu;
                }
                catch
                {
                    codesPanel.ContextMenu = null;
                }
            };
        }

        /// <summary>Call this when a new patch file is loaded so the snapshot resets.</summary>
        public static void NotifyNewFileLoaded(string fullFileText)
        {
            _originalFileTextSnapshot = string.IsNullOrWhiteSpace(fullFileText) ? null : fullFileText;
        }

        private static void TrySnapshotOriginal(Func<string> getFullFileText)
        {
            if (!string.IsNullOrWhiteSpace(_originalFileTextSnapshot)) return;
            var txt = SafeGet(getFullFileText);
            if (!string.IsNullOrWhiteSpace(txt))
                _originalFileTextSnapshot = txt;
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f?.Invoke() ?? string.Empty; } catch { return string.Empty; }
        }

        // ----- Guards & filtering -----

        private static bool IsModEntry(SavepatchText.CodeBlock cb)
{
    if (cb == null) return false;
    if (IsModsHeader(cb)) return false;

    var name = cb.Name ?? string.Empty;
    bool hasPrefix = name.TrimStart().StartsWith("-M-", StringComparison.OrdinalIgnoreCase);
    bool hasTokens = HasAnyToken(cb.Lines);
    bool hasAmount = cb.Lines != null && cb.Lines.Any(l => AmountTokenRegex.IsMatch(l));

    return hasPrefix || hasTokens || hasAmount;
}
        

// Unified entry: handles {AMOUNT:...} tokens directly; if classic {MOD} tokens exist, falls back to legacy path for them.
private static void RunOpenModValuesFlowUnified(ItemsControl codesPanel,
                                                SavepatchText.CodeBlock cb,
                                                string codeName,
                                                string fileText,
                                                Action<SavepatchText.CodeBlock, string>? replaceFirstTokenLine,
                                                Action<SavepatchText.CodeBlock, string?>? appendAppliedName)
{
    var lines = cb.Lines ?? new List<string>();
    bool anyAmount = lines.Any(l => AmountTokenRegex.IsMatch(l));
    bool anyMod = lines.Any(l => TokenRegex.IsMatch(l));

    if (!anyAmount && !anyMod)
    {
        MessageBox.Show("No Place to Add Mods, Please Try Another Code", "Open MOD Values", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }

    // Handle AMOUNT tokens first, in visual order
    if (anyAmount)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i] ?? string.Empty;
            while (true)
            {
                var m = AmountTokenRegex.Match(l);
                if (!m.Success) break;

                // Parse defaults and show dialog
                var def = (m.Groups["def"].Success ? m.Groups["def"].Value : string.Empty) ?? string.Empty;
                var type = (m.Groups["type"].Success ? m.Groups["type"].Value : string.Empty).ToUpperInvariant();
                var endian = (m.Groups["endian"].Success ? m.Groups["endian"].Value : string.Empty).ToUpperInvariant();
                if (string.IsNullOrEmpty(type)) type = "HEX";
                if (string.IsNullOrEmpty(endian)) endian = "BIG";

                // Compute initial decimal text from default
                string initial;
                try
                {
                    if (type == "FLOAT")
                    {
                        // Default HEX string -> float -> decimal display
                        var s = def;
                        if (string.IsNullOrWhiteSpace(s)) s = "00000000";
                        if (s.Length % 2 == 1) s = "0" + s;
                        byte[] b = Enumerable.Range(0, s.Length / 2).Select(k => byte.Parse(s.Substring(k*2,2), NumberStyles.HexNumber)).ToArray();
                        if (string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase)) Array.Reverse(b);
                        if (b.Length < 4) b = b.Concat(new byte[4 - b.Length]).ToArray();
                        if (b.Length > 4) b = b.Take(4).ToArray();
                        float fv = BitConverter.ToSingle(b, 0);
                        initial = fv.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        var s = def;
                        if (string.IsNullOrWhiteSpace(s)) s = "00000000";
                        if (s.Length % 2 == 1) s = "0" + s;
                        byte[] b = Enumerable.Range(0, s.Length / 2).Select(k => byte.Parse(s.Substring(k*2,2), NumberStyles.HexNumber)).ToArray();
                        if (string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase)) Array.Reverse(b);
                        if (b.Length < 4) b = b.Concat(new byte[4 - b.Length]).ToArray();
                        if (b.Length > 4) b = b.Take(4).ToArray();
                        uint iv = BitConverter.ToUInt32(b, 0);
                        initial = iv.ToString(CultureInfo.InvariantCulture);
                    }
                }
                catch { initial = "0"; }
                
                if (string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase))
                {
                    initial = string.Empty;
                }
    
                // Max length for dialog (text => default string length; 'NA' => unlimited)
                int? maxLen = null;
// Compute allowed max length (NA => unlimited; text by default length)
if (!string.IsNullOrWhiteSpace(def) && !string.Equals(def, "NA", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase))
                    {
                        maxLen = def.Length;
                    }
                    else if (int.TryParse(def, NumberStyles.Integer, CultureInfo.InvariantCulture, out var _n) && _n > 0)
                    {
                        maxLen = _n;
                    }
                }
    

                
                // Compute allowed max length (NA => unlimited; text by default string length)
var __defTrim = def?.Trim();
                if (!string.IsNullOrEmpty(__defTrim) && !string.Equals(__defTrim, "NA", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase))
                    {
                        maxLen = __defTrim.Length;
                    }
                    else if (int.TryParse(__defTrim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var __n) && __n > 0)
                    {
                        maxLen = __n;
                    }
                }

                var dlg = new AmountPromptWindow(
                    type, endian, initial, def,
                    allowTypeAndEndianSelection: !(string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) && string.Equals(endian, "TXT", StringComparison.OrdinalIgnoreCase))
                                                 && !string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase)
                                                 && !string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase));
                dlg.ConfigureMaxLength(maxLen);

                dlg.Owner = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault(w => w.IsActive);
                if (dlg.ShowDialog() != true) return; // canceled
                if (maxLen.HasValue && (string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase)))
                {
                    var _slen = (dlg.ResultValue ?? string.Empty).Length;
                    if (_slen > maxLen.Value)
                    {
                        MessageBox.Show($"Max length is {maxLen.Value} characters for this MOD.", "Special MOD", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
    

                // Compute result hex
                string hex;
                try
                {
                    
                    // Text modes first
                    if (string.Equals(type, "ABC123", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(endian, "TXT", StringComparison.OrdinalIgnoreCase))
                    {
                        // Direct text insertion (no hex conversion)
                        hex = dlg.ResultValue ?? string.Empty;
                    }
                    else if (string.Equals(type, "UTF08", StringComparison.OrdinalIgnoreCase))
                    {
                        var sVal = dlg.ResultValue ?? string.Empty;
                        var bytes = System.Text.Encoding.UTF8.GetBytes(sVal);
                        if (string.Equals(endian, "LITTLE", StringComparison.OrdinalIgnoreCase))
                            System.Array.Reverse(bytes);
                        hex = string.Concat(bytes.Select(bb => bb.ToString("X2")));
                    }
                    else if (string.Equals(type, "UTF16", StringComparison.OrdinalIgnoreCase))
                    {
                        var sVal = dlg.ResultValue ?? string.Empty;
                        var enc = string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase)
                            ? System.Text.Encoding.BigEndianUnicode
                            : System.Text.Encoding.Unicode;
                        var bytes = enc.GetBytes(sVal);
                        hex = string.Concat(bytes.Select(bb => bb.ToString("X2")));
                    }
                    else if (string.Equals(type, "FLOAT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!float.TryParse(dlg.ResultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return;
                        var bytes = BitConverter.GetBytes(f);
                        if (string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase)) Array.Reverse(bytes);
                        hex = string.Concat(bytes.Select(bb => bb.ToString("X2")));
                    }
                    else
                    {
                        if (!ulong.TryParse(dlg.ResultValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return;
                        int tokenWidthBytes = 0;
                if (!string.IsNullOrEmpty(def) && !string.Equals(def, "NA", StringComparison.OrdinalIgnoreCase))
                    tokenWidthBytes = Math.Min(8, Math.Max(1, def.Length / 2));
                if (tokenWidthBytes == 0) tokenWidthBytes = 4;
                var big = string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase);
                var bytes = BuildBytesForValue(n, tokenWidthBytes);
if (string.Equals(endian, "BIG", StringComparison.OrdinalIgnoreCase))
    Array.Reverse(bytes);
hex = string.Concat(bytes.Select(bb => bb.ToString("X2")));
                    }
    }
                catch { return; }

                // Replace this AMOUNT token with the computed hex
                l = l.Remove(m.Index, m.Length).Insert(m.Index, hex);
                lines[i] = l;
                cb.Lines[i] = l;
                replaceFirstTokenLine?.Invoke(cb, l);
                cb.Lines = new List<string>(cb.Lines);
                ForceRefreshItem(codesPanel, cb);
                AppendSuffixOrFallback(codesPanel, cb, codeName, dlg.ResultValue, appendAppliedName);

            }
        }
    }

    // If any classic MOD tokens remain, delegate to legacy handler for those
    if (lines.Any(ln => TokenRegex.IsMatch(ln)))
    {
        RunFillAllModsFlow(codesPanel, cb, codeName, fileText, replaceFirstTokenLine, appendAppliedName);
    }

            try
            {
                codesPanel.Dispatcher.BeginInvoke(new Action(() => RestoreInteraction(codesPanel)), DispatcherPriority.Background);
            }
            catch { }
    }

        private static void InstallItemsSourceFilter(ItemsControl panel)
        {
            try
            {
                ApplyModsHeaderFilter(panel);
                var dpd = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ItemsControl));
                dpd?.AddValueChanged(panel, (s, e) => ApplyModsHeaderFilter(panel));
            }
            catch { /* ignore */ }
        }

        private static void ApplyModsHeaderFilter(ItemsControl panel)
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(panel.ItemsSource);
                if (view == null) return;
                view.Filter = o =>
                {
                    var cb = o as SavepatchText.CodeBlock;
                    if (cb == null) return true;
                    return !IsModsHeader(cb);
                };
                view.Refresh();
            }
            catch { /* ignore */ }
        }

        private static bool IsModsHeader(SavepatchText.CodeBlock? cb)
        {
            if (cb == null) return false;
            var name = (cb.Name ?? string.Empty).Trim();
            if (name.StartsWith("-M-", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(3).TrimStart();
            return string.Equals(name, "MODS:", StringComparison.OrdinalIgnoreCase);
        }

        // ----- Fill flow -----

        private static void RunFillAllModsFlow(
            ItemsControl codesPanel,
            SavepatchText.CodeBlock cb,
            string codeName,
            string fileText,
            Action<SavepatchText.CodeBlock, string>? replaceFirstTokenLine,
            Action<SavepatchText.CodeBlock, string?>? appendAppliedName)
        {
            var blocks = ModBlockParser.ParseAll(fileText);

            while (true)
            {
                if (!FindFirstTokenOccurrence(cb.Lines, out int lineIdx, out int start, out string tag))
                {
                    MessageBox.Show("No {MOD} tokens remain in this code.", "Fill MODs", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                }

                if (!blocks.TryGetValue(tag, out var block))
                {
                    var res = MessageBox.Show($"No block {{{tag}}} found. Skip and continue?",
                                              "Fill MODs", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (res == MessageBoxResult.Yes)
                    {
                        string line = cb.Lines[lineIdx];
                        cb.Lines[lineIdx] = RemoveOneTokenAtIndex(line, start);
                        cb.Lines = new List<string>(cb.Lines);
                        ForceRefreshItem(codesPanel, cb);
                
                        replaceFirstTokenLine?.Invoke(cb, cb.Lines[lineIdx]);
                        continue;
                    }
                    break;
                }

                string caption = $"{codeName} — {tag}";
                var (val, disp) = PromptForRow(tag, block, caption);
                if (string.IsNullOrEmpty(val)) break; // cancelled

                string oldLine = cb.Lines[lineIdx];
                string newLine = ReplaceOneOccurrenceAtIndex(oldLine, start, val);
                cb.Lines[lineIdx] = newLine;

                replaceFirstTokenLine?.Invoke(cb, newLine);

                cb.Lines = new List<string>(cb.Lines);
                ForceRefreshItem(codesPanel, cb);
                AppendSuffixOrFallback(codesPanel, cb, codeName, string.IsNullOrWhiteSpace(disp) ? tag : disp, appendAppliedName);


                appendAppliedName?.Invoke(cb, disp);
            }
        
            try
            {
                codesPanel.Dispatcher.BeginInvoke(new Action(() => RestoreInteraction(codesPanel)), DispatcherPriority.Background);
            }
            catch { }
    }

        // ----- Restore by header name with preference to tokenized section -----

        

private static void RestoreInteraction(ItemsControl panel)
{
    try { Mouse.Capture(null); } catch { }
    try { Keyboard.ClearFocus(); } catch { }
    if (panel is UIElement el)
    {
        try { el.Focus(); } catch { }
    }
}

        // Appends a human suffix to the code’s display name.
// Uses the host-provided appendAppliedName when available; otherwise also updates cb.Name and refreshes.

private static void BackupOriginal(SavepatchText.CodeBlock cb)
{
    try
    {
        if (cb?.Lines == null) return;
        if (!s_OriginalTokenizedByIndex.ContainsKey(cb.Index))
        {
            bool any = cb.Lines.Any(l => TokenRegex.IsMatch(l) || AmountTokenRegex.IsMatch(l));
            if (any)
                s_OriginalTokenizedByIndex[cb.Index] = new List<string>(cb.Lines);
        }
    }
    catch { }
}

private static void AppendSuffixOrFallback(
    ItemsControl codesPanel,
    SavepatchText.CodeBlock cb,
    string codeName,
    string newPiece,
    Action<SavepatchText.CodeBlock, string?>? appendAppliedName)
{
    if (string.IsNullOrWhiteSpace(newPiece)) return;

    // Try host callback too (but do not return early)
    try { appendAppliedName?.Invoke(cb, newPiece); } catch { }

    string baseName = codeName ?? string.Empty;
    int paren = baseName.LastIndexOf(" (", StringComparison.Ordinal);
    if (paren > 0) baseName = baseName.Substring(0, paren);

    string mergedSuffix;
    if (!string.IsNullOrEmpty(cb.Name) && cb.Name.Contains(" (") && cb.Name.EndsWith(")"))
    {
        int open = cb.Name.LastIndexOf(" (", StringComparison.Ordinal);
        string existing = (open > -1) ? cb.Name.Substring(open + 2, cb.Name.Length - open - 3) : string.Empty; // inside (...)
        var parts = existing.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
        if (!parts.Contains(newPiece)) parts.Add(newPiece);
        mergedSuffix = string.Join("; ", parts);
    }
    else
    {
        mergedSuffix = newPiece;
    }

    cb.Name = $"{baseName} ({mergedSuffix})";
    ForceRefreshItem(codesPanel, cb);
}

private static List<string> ExtractOriginalCodeLines_ByHeaderNamePreferTokenized(string fullText, string cleanHeaderName, int currentHeaderOrdinal)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(fullText) || string.IsNullOrWhiteSpace(cleanHeaderName)) return result;

            var raw = SplitLines(fullText);

            // Build list of all headers with ordinals
            var headerPositions = new List<(int LineIndex, string Name, int Ordinal)>();
            int ordinal = 0;
            for (int i = 0; i < raw.Count; i++)
            {
                var m = HeaderLineRegex.Match((raw[i] ?? string.Empty).Trim());
                if (m.Success)
                {
                    var n = (m.Groups["h"].Value ?? string.Empty).Trim();
                    headerPositions.Add((i, n, ordinal));
                    ordinal++;
                }
            }
            if (headerPositions.Count == 0) return result;

            // Collect candidates with the same clean name
            var candidates = new List<(int Start, int End, List<string> Lines, bool HasToken, int Ordinal)>();

            for (int i = 0; i < headerPositions.Count; i++)
            {
                var h = headerPositions[i];
                if (!string.Equals(CleanHeaderName(h.Name), cleanHeaderName, StringComparison.OrdinalIgnoreCase))
                    continue;

                int start = h.LineIndex + 1;
                int end = raw.Count;
                // next header position
                for (int j = i + 1; j < headerPositions.Count; j++)
                {
                    if (headerPositions[j].LineIndex > h.LineIndex)
                    {
                        end = Math.Min(end, headerPositions[j].LineIndex);
                        break;
                    }
                }

                var codeOnly = ExtractCodeOnlyBetween(raw, start, end);
                bool hasTok = HasAnyToken(codeOnly);
                candidates.Add((start, end, codeOnly, hasTok, h.Ordinal));
            }

            if (candidates.Count == 0) return result;

            // Prefer tokenized; then nearest by ordinal to current
            var tokenized = candidates.Where(c => c.HasToken).ToList();
            var shortlist = tokenized.Count > 0 ? tokenized : candidates;

            var chosen = shortlist.OrderBy(c => Math.Abs(c.Ordinal - currentHeaderOrdinal)).First();
            return chosen.Lines;
        }

        private static string CleanHeaderName(string? name)
        {
            var n = (name ?? string.Empty).Trim();
            if (n.StartsWith("-M-", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(3).TrimStart();
            return n;
        }

        private static List<string> ExtractCodeOnlyBetween(List<string> raw, int start, int endExclusive)
        {
            var result = new List<string>();
            bool seenAny = false;

            for (int i = start; i < endExclusive; i++)
            {
                var row = raw[i];
                var t = (row ?? string.Empty).TrimEnd();

                if (IsHeaderLine(t)) break;
                if (IsModBlockHeaderLine(t)) break;

                if (string.IsNullOrWhiteSpace(t))
                {
                    if (seenAny)
                    {
                        int k = i + 1;
                        while (k < endExclusive && string.IsNullOrWhiteSpace(raw[k])) k++;
                        if (k < endExclusive && IsCodeLine((raw[k] ?? string.Empty).TrimEnd()))
                        {
                            result.Add(row);
                            continue;
                        }
                        else break;
                    }
                    else continue;
                }

                if (IsCodeLine(t))
                {
                    result.Add(row);
                    seenAny = true;
                    continue;
                }

                break;
            }

            while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1])) result.RemoveAt(result.Count - 1);
            return result;
        }

        private static List<string> SplitLines(string text)
            => text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();

        // ----- Utilities -----

        public static void ApplyModPrefixes(
            IEnumerable<object> items,
            Func<object, string> getSampleLine,
            Func<object, string> getName,
            Action<object, string?> setName,
            string fileText)
        {
            if (items == null) return;
            foreach (var it in items)
            {
                string name = getName?.Invoke(it) ?? string.Empty;
                if (IsModsName(name)) continue; // never prefix MODS:

                string line = getSampleLine?.Invoke(it) ?? string.Empty;
                if (!TokenRegex.IsMatch(line)) continue;

                if (name.StartsWith("-M-", StringComparison.OrdinalIgnoreCase)) continue;

                string prefixed = name.StartsWith(" ") ? "-M-" + name : "-M- " + name;
                setName?.Invoke(it, prefixed);
            }
        }

        private static bool IsModsName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.StartsWith("-M-", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(3).TrimStart();
            return string.Equals(n, "MODS:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasAnyToken(IList<string> lines)
        {
            if (lines == null) return false;
            foreach (var l in lines)
            {
                if (string.IsNullOrEmpty(l)) continue;
                if (TokenRegex.IsMatch(l)) return true;
            }
            return false;
        }
		private static bool HasAnyTokenOrAmount(IList<string> lines)
{
    if (lines == null) return false;
    foreach (var l in lines)
    {
        if (string.IsNullOrEmpty(l)) continue;
        if (TokenRegex.IsMatch(l) || AmountTokenRegex.IsMatch(l))
            return true;
    }
    return false;
}

        private static bool FindFirstTokenOccurrence(IList<string> lines, out int lineIdx, out int start, out string tag)
        {
            lineIdx = -1; start = -1; tag = string.Empty;
            if (lines == null) return false;

            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i] ?? string.Empty;
                foreach (Match m in TokenRegex.Matches(l))
                {
                    lineIdx = i;
                    start = m.Index;
                    tag = m.Groups["n"].Value;
                    return true;
                }
            }
            return false;
        }

        private static bool IsHeaderLine(string t) => t.StartsWith("[") && t.Contains("]") && t.IndexOf(']') > 1;
        private static bool IsModBlockHeaderLine(string t) => ModHeaderRegex.IsMatch(t);
        private static bool IsCodeLine(string t) => CodeLineRegex.IsMatch(t);

        private static (string Value, string? Display) PromptForRow(string tag, ModBlock block, string caption)
        {
            if (block.Headers == null || block.Headers.Count <= 1)
            {
                var rows = block.Rows.Select(r => new List<string> { r.ElementAtOrDefault(0) ?? string.Empty, r.ElementAtOrDefault(1) ?? string.Empty }).ToList();
                var dlg = new ModSelectDialog(tag, new List<string> { "Value" }, rows, caption);
                dlg.Owner = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault(w => w.IsActive);
                return dlg.ShowDialog() == true
                    ? (dlg.SelectedValue ?? string.Empty, dlg.SelectedName)
                    : (string.Empty, null);
            }
            else
            {
                var dlg = new ModSelectDialog(tag, block.Headers, block.Rows, caption);
                dlg.Owner = Application.Current?.Windows?.OfType<Window>()?.FirstOrDefault(w => w.IsActive);
                return dlg.ShowDialog() == true
                    ? (dlg.SelectedValue ?? string.Empty, dlg.SelectedName)
                    : (string.Empty, null);
            }
        }

        private static string ReplaceOneOccurrenceAtIndex(string tpl, int leftBraceIndex, string replacement)
        {
            int r = tpl.IndexOf('}', leftBraceIndex + 1);
            if (r < 0) return tpl;
            return tpl.Substring(0, leftBraceIndex) + replacement + tpl.Substring(r + 1);
        }

        private static string RemoveOneTokenAtIndex(string tpl, int leftBraceIndex)
        {
            int r = tpl.IndexOf('}', leftBraceIndex + 1);
            if (r < 0) return tpl;
            return tpl.Remove(leftBraceIndex, r - leftBraceIndex + 1);
        }

        private static void ForceRefreshItem(ItemsControl panel, object item)
        {
            try
            {
                var container = panel.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var dc = container.DataContext;
                    container.DataContext = null;
                    container.DataContext = dc;
                }
                panel.Items.Refresh();
            }
            catch { /* ignore */ }
        }

        private static T? FindDataContext<T>(DependencyObject current) where T : class
        {
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
