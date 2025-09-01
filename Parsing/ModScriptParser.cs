// ============================================================================
//  ApolloGUI — ModScriptParser.cs
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

namespace ApolloGUI.Parsing
{
    public sealed class ModOption
    {
        public string Value { get; init; } = string.Empty;
        public string Name  { get; init; } = string.Empty;
        public string? Type { get; init; } = null;
        public override string ToString() => Type is null ? $"{Value} | {Name}" : $"{Value} | {Name} | {Type}";
    }

    public sealed class ModDefinition
    {
        public string Id { get; init; } = string.Empty;
        public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
        public List<ModOption> Options { get; } = new List<ModOption>();
        public override string ToString() => $"{Id} ({string.Join(">", Headers)}) — {Options.Count} options";
    }

    /// <summary>
    /// Robust parser for [MODS:] sections with {ID}...{\ID} blocks.
    /// </summary>
    public static class ModScriptParser
    {
        private static readonly Regex ModsHeaderRx = new Regex(@"^\s*\[\s*MODS\s*:\s*\]\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BlockStartRx = new Regex(@"^\s*\{\s*([A-Za-z0-9_]+)\s*\}\s*$",
            RegexOptions.Compiled);

        private static readonly Regex BlockEndRx = new Regex(@"^\s*\{\\\s*([A-Za-z0-9_]+)\s*\}\s*$",
            RegexOptions.Compiled);

        public static IReadOnlyDictionary<string, ModDefinition> Parse(string fullText)
        {
            if (fullText == null) throw new ArgumentNullException(nameof(fullText));

            var lines = NormalizeNewlines(fullText).Split('\n');
            var dict = new Dictionary<string, ModDefinition>(StringComparer.OrdinalIgnoreCase);

            int start = Array.FindIndex(lines, l => ModsHeaderRx.IsMatch(l));
            if (start < 0) return dict;

            int i = start + 1;
            while (i < lines.Length)
            {
                var mStart = MatchAt(BlockStartRx, lines, ref i);
                if (mStart == null) break;
                string id = mStart.Groups[1].Value.Trim();
                var def = new ModDefinition { Id = id };

                bool headerConsumed = false;
                while (i < lines.Length)
                {
                    var line = lines[i].TrimEnd();
                    if (string.IsNullOrWhiteSpace(line)) { i++; continue; }
                    if (BlockEndRx.IsMatch(line)) break;

                    if (!headerConsumed && line.Contains('>'))
                    {
                        def = new ModDefinition { Id = id, Headers = ParseHeaders(line) };
                        headerConsumed = true;
                        i++;
                        continue;
                    }
                    break;
                }

                if (def.Headers.Count == 0)
                    def = new ModDefinition { Id = id, Headers = new[] { "VALUE", "NAME" } };

                while (i < lines.Length)
                {
                    var line = lines[i].TrimEnd();
                    if (BlockEndRx.IsMatch(line)) { i++; break; }
                    if (BlockStartRx.IsMatch(line)) break;
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";")) { i++; continue; }

                    var opt = TryParseOption(line, def.Headers);
                    if (opt != null) def.Options.Add(opt);
                    i++;
                }

                dict[id] = def;
            }
            return dict;
        }

        private static string NormalizeNewlines(string s) =>
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");

        private static Match? MatchAt(Regex rx, string[] lines, ref int i)
        {
            while (i < lines.Length)
            {
                var m = rx.Match(lines[i]);
                if (m.Success) { i++; return m; }
                i++;
            }
            return null;
        }

        private static IReadOnlyList<string> ParseHeaders(string headerLine)
        {
            return headerLine.Split('>')
                             .Select(x => x.Trim().ToUpperInvariant())
                             .Where(x => x.Length > 0)
                             .ToArray();
        }

        private static ModOption? TryParseOption(string line, IReadOnlyList<string> headers)
        {
            if (headers.Count >= 3)
            {
                var tokens = TokenizeMax(line, 3);
                if (tokens.Length < 3) return null;
                return new ModOption { Value = tokens[0], Name = tokens[1], Type = tokens[2] };
            }
            else
            {
                var tokens = TokenizeMax(line, 2);
                if (tokens.Length < 2) return null;
                return new ModOption { Value = tokens[0], Name = tokens[1], Type = null };
            }
        }

        private static string[] TokenizeMax(string line, int maxParts)
        {
            var raw = Regex.Split(line.Trim(), @"\s+");
            if (raw.Length <= maxParts) return raw;

            if (maxParts == 3)
            {
                var first = raw[0];
                var last  = raw[^1];
                var middle = string.Join(" ", raw.Skip(1).Take(raw.Length - 2));
                return new[] { first, middle, last };
            }
            else
            {
                var first = raw[0];
                var rest  = string.Join(" ", raw.Skip(1));
                return new[] { first, rest };
            }
        }
    }
}
