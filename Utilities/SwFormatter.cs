// ============================================================================
//  ApolloGUI — SwFormatter.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================


using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ApolloGUI.Utils
{
    /// <summary>
    /// Utilities for formatting Save Wizard-style hex blocks.
    /// </summary>
    public static class SwFormatter
    {
        private static readonly Regex OnlyHexAndWhitespace =
            new Regex(@"^(?:\s|[0-9A-Fa-f])+$", RegexOptions.Compiled);

        private static readonly Regex HexToken8 =
            new Regex(@"[0-9A-Fa-f]{8}", RegexOptions.Compiled);

        /// <summary>
        /// Collector-only intent: Reflow *per input line*.
        /// For each line that contains only hex + whitespace, chunk into pairs of 8-hex tokens.
        /// If a line ends with a single token, pad the second half with 00000000.
        /// Lines that contain anything else are left unchanged.
        /// </summary>
        public static string NormalizeSwBlocksForCollector(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var src = text.Replace("\r", string.Empty);
            var lines = src.Split('\n');
            var sbOut = new StringBuilder(text.Length + 64);
            bool anyChange = false;

            foreach (var raw in lines)
            {
                var line = raw ?? string.Empty;
                var trimmed = line.Trim();

                if (trimmed.Length > 0 && OnlyHexAndWhitespace.IsMatch(trimmed) && HexToken8.IsMatch(trimmed))
                {
                    var matches = HexToken8.Matches(trimmed);
                    for (int i = 0; i < matches.Count; i += 2)
                    {
                        string left  = matches[i].Value.ToUpperInvariant();
                        string right = (i + 1 < matches.Count) ? matches[i + 1].Value.ToUpperInvariant() : "00000000";
                        sbOut.Append(left).Append(' ').Append(right).Append('\n');
                    }
                    anyChange = true;
                }
                else
                {
                    sbOut.Append(line).Append('\n');
                }
            }

            // Trim a single trailing newline to avoid creating a spurious empty line on split
            if (sbOut.Length > 0 && sbOut[^1] == '\n')
                sbOut.Length--;

            return anyChange ? sbOut.ToString() : text;
        }
    }
}
