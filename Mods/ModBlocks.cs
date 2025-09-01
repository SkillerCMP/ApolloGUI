// ============================================================================
//  ApolloGUI — ModBlocks.cs
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

namespace ApolloGUI
{
    /// <summary>
    /// One MOD block: name, headers, and data rows.
    /// </summary>
    public sealed class ModBlock
    {
        public string Name { get; }
        public List<string> Headers { get; }
        public List<List<string>> Rows { get; }

        public ModBlock(string name, List<string> headers, List<List<string>> rows)
        {
            Name = name;
            Headers = headers;
            Rows = rows;
        }
    }

    /// <summary>
    /// Parser for {MOD} blocks (CMP-compatible):
    /// - Header line uses '>' as column separator (e.g., Value>Name>Type).
    /// - Data rows accept TAB, '=' (pair), '>' (multi-col), or 2+ spaces; fallback: single space.
    /// - First column is always Value; if only one header and rows look like pairs, infer Value>Name.
    /// - Helpers: trim preview above first bare block tag; extract inline token names.
    /// </summary>
    public static class ModBlockParser
    {
        // IMPORTANT: match only BARE tag lines as block start/end (not inline tokens)
        private static readonly Regex BlockRegex = new(
            @"^[ \t]*\{(?<name>[A-Za-z0-9_]+)\}[ \t]*(?:\r?\n|\r|\n)(?<body>[\s\S]*?)^[ \t]*\{[\/\\]\k<name>\}[ \t]*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Matches inline tokens like {MOD} inside code lines
        private static readonly Regex TokenRegex =
            new(@"\{(?<n>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);

        public static Dictionary<string, ModBlock> ParseAll(string? fileText)
        {
            var map = new Dictionary<string, ModBlock>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(fileText)) return map;
// Normalize line endings and strip BOM/ZW spaces so anchors work consistently
fileText = fileText.Replace("\r\n", "\n").Replace("\r", "\n")
                   .Replace("\uFEFF", string.Empty)  // BOM
                   .Replace("\u200B", string.Empty); // zero-width space
            foreach (Match m in BlockRegex.Matches(fileText))
            {
                string name = m.Groups["name"].Value;
                string body = m.Groups["body"].Value;
                var parsed = ParseSingleBlock(name, body);
if (map.TryGetValue(name, out var existing))
{
    bool newIsBetter = (parsed.Headers?.Count ?? 0) > (existing.Headers?.Count ?? 0)
                       || (parsed.Rows?.Count ?? 0) > (existing.Rows?.Count ?? 0);
    if (newIsBetter) map[name] = parsed;
}
else map[name] = parsed;
            }
            return map;
        }

        /// <summary>
        /// Extract all inline token names (e.g., {MOD}, {TIM2}) from a sequence of code lines.
        /// </summary>
        public static List<string> ExtractReferencedBlocksFromLines(IEnumerable<string?> lines)
        {
            var names = new List<string>();
            if (lines == null) return names;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                foreach (Match m in TokenRegex.Matches(line))
                    names.Add(m.Groups["n"].Value);
            }
            return names;
        }

        /// <summary>
        /// For code preview lists: stop output at the first bare MOD tag line.
        /// Does NOT remove inline tokens like "50000000 {MOD}".
        /// </summary>
        public static IEnumerable<string> TrimCodeLinesForPreview(IEnumerable<string?> lines)
        {
            if (lines == null) yield break;
            foreach (var l in lines)
            {
                if (IsBareBlockTag(l)) yield break;
                yield return l ?? string.Empty;
            }
        }

        public static bool IsBareBlockTag(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;
            var s = line.Trim();
            return Regex.IsMatch(s, @"^\{[\/\\]?[A-Za-z0-9_]+\}$");
        }

        private static ModBlock ParseSingleBlock(string name, string body)
        {
            var norm = (body ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            var all = norm.Split('\n');
            var content = all.Select(l => l?.TrimEnd() ?? string.Empty)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToList();
            if (content.Count == 0)
                return new ModBlock(name, new List<string> { "Value" }, new List<List<string>>());

            int hdrIdx = 0;
// Find the first plausible header line (contains '>'); skip leading code lines if present
for (int _i = 0; _i < content.Count; _i++)
{
    var _ln = (content[_i] ?? string.Empty).Trim();
    if (_ln.Length == 0 || _ln.StartsWith(";")) continue;
    if (_ln.Contains(">")) { hdrIdx = _i; break; }
}
var headerRaw = content[hdrIdx].Trim();
            var headers = headerRaw.Split('>')
                                   .Select(h => (h ?? string.Empty).Trim())
                                   .Where(h => h.Length > 0)
                                   .ToList();
            if (headers.Count == 0) headers = new List<string> { "Value" };

            // Infer Value>Name if single header and first row is a pair
            if (headers.Count == 1 && content.Count > hdrIdx + 1)
            {
                var probe = content[1];
                if (probe.Contains('\t') || probe.Contains('='))
                    headers = new List<string> { "Value", "Name" };
            }

            var rows = new List<List<string>>();
            for (int i = hdrIdx + 1; i < content.Count; i++)
            {
                var ln = content[i];
                if (ln.StartsWith(";")) continue; // comment support
                rows.Add(SplitRow(ln, headers.Count));
            }

            return new ModBlock(name, headers, rows);
        }

        /// <summary>
        /// CMP-like row splitting:
        ///   Priority: TAB -> '=' (pair) -> '>' (multi-col rows) -> 2+ spaces -> fallback single spaces
        ///   Always trims cells and pads to headerCount columns.
        /// </summary>
        private static List<string> SplitRow(string line, int headerCount)
        {
            string[] parts;

            if (line.Contains('\t'))
            {
                parts = line.Split('\t');
            }
            else if (line.Contains('='))
            {
                // Pair form: VALUE=NAME (split once)
                parts = line.Split(new[] { '=' }, 2);
            }
            else if (line.Contains('>'))
            {
                // Some authors mirror header separators in rows; support it
                parts = line.Split('>');
            }
            else
            {
                // Try 2+ spaces as column break; then final fallback to single spaces
                var bySpace2 = Regex.Split(line.Trim(), @"\s{2,}");
                parts = (bySpace2.Length > 1) ? bySpace2 : line.Trim().Split(' ');
            }

            var list = parts.Select(p => (p ?? string.Empty).Trim()).ToList();

            // Pad to headerCount so columns align
            if (headerCount > 0)
                while (list.Count < headerCount) list.Add(string.Empty);

            return list;
        }
    }
}
