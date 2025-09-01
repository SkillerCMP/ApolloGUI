// ============================================================================
//  ApolloGUI — ModPrefixConverter.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

// Mods/ModPrefixConverter.cs (AMOUNT-aware, string-safe, prefix-dedup) — CLEAN
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace ApolloGUI
{
    public sealed class ModPrefixConverter : IValueConverter
    {
// ----- LINT NOTES (2025-08-31) -----------------------------------------------
// Prefix/Suffix generation:
// - Keep this transformation pure (no side effects) so UI can safely re-run it on change.
// - If prefix depends on active MODs, include the MOD list in the input to avoid hidden state.
// - Validate hex strings before concatenation; prefer uppercase normalized hex.
// ------------------------------------------------------------------------------

        // Detects {AMOUNT[:...]} in code lines
        private static readonly Regex AmountToken = new Regex(@"\{AMOUNT(?::[^}]*)?\}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Match headers like [Block Name]
        private static readonly Regex HeaderRegex = new Regex(@"^\[(?<name>[^\]]+)\]\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string displayName = value?.ToString() ?? string.Empty;
            IList<string>? lines = null;

            if (value is SavepatchText.CodeBlock cb)
            {
                displayName = cb.Name ?? string.Empty;
                lines = cb.Lines;
            }
            else
            {
                // Try SavepatchText lookup; fall back to header scan of the raw text
                var text = CheatFileContext.CurrentText ?? string.Empty;
                try
                {
                    var parsed = SavepatchText.Parse(text);
                    var key = displayName.StartsWith("-M-", StringComparison.OrdinalIgnoreCase)
                        ? displayName.Substring(3).TrimStart()
                        : displayName;
                    var found = parsed.Codes.FirstOrDefault(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));
                    lines = found?.Lines ?? TryGetLinesByHeader(text, key);
                }
                catch
                {
                    lines = TryGetLinesByHeader(text, displayName);
                }
            }

            // Classic MOD detection (via referenced tokens)
            var blocks = ModBlockParser.ParseAll(CheatFileContext.CurrentText);
            var tokens = (lines != null) ? ModBlockParser.ExtractReferencedBlocksFromLines(lines) : new List<string>();
            bool hasClassic = tokens.Any(t => blocks.ContainsKey(t));

            // Special AMOUNT detection
            bool hasSpecial = lines != null && lines.Any(l => l != null && AmountToken.IsMatch(l));

            // Avoid double prefix if displayName already has "-M- "
            var baseName = displayName.StartsWith("-M-", StringComparison.OrdinalIgnoreCase)
                ? displayName.Substring(3).TrimStart()
                : displayName;

            return (hasClassic || hasSpecial) ? "-M- " + baseName : baseName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;

        private static IList<string>? TryGetLinesByHeader(string text, string displayName)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(displayName))
                return null;

            // Strip -M- for lookup if necessary
            var key = displayName.StartsWith("-M-", StringComparison.OrdinalIgnoreCase)
                ? displayName.Substring(3).TrimStart()
                : displayName;

            var matches = HeaderRegex.Matches(text);
            if (matches.Count == 0)
                return null;

            Match? chosen = null;
            foreach (Match m in matches)
            {
                var nm = (m.Groups["name"].Value ?? string.Empty).Trim();
                if (string.Equals(nm, key, StringComparison.OrdinalIgnoreCase))
                {
                    chosen = m; break;
                }
            }
            if (chosen == null) return null;

            int start = chosen.Index + chosen.Length;
            int end = text.Length;
            foreach (Match m in matches)
            {
                if (m.Index > chosen.Index)
                {
                    end = m.Index;
                    break;
                }
            }

            var slice = text.Substring(start, end - start);
            slice = slice.Replace("\r\n", "\n");
            var arr = slice.Split('\n');

            var list = new List<string>();
            foreach (var raw in arr)
            {
                var line = raw ?? string.Empty;
                // stop if we accidentally encountered another header due to malformed text
                if (line.StartsWith("[") && line.EndsWith("]")) break;
                list.Add(line);
            }
            // Trim trailing blanks
            while (list.Count > 0 && string.IsNullOrWhiteSpace(list[list.Count - 1]))
                list.RemoveAt(list.Count - 1);
            return list;
        }
    }
}