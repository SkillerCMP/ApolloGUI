// ============================================================================
//  ApolloGUI — ModTokenFinder.cs
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
using System.Text.RegularExpressions;

namespace ApolloGUI.Mods
{
    public static class ModTokenFinder
    {
        private static readonly Regex TokenRx = new Regex(@"\{(?!\\)([A-Za-z0-9_]+)\}",
            RegexOptions.Compiled);

        public static IReadOnlyList<string> Find(string codeLine)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(codeLine)) return list;
            var m = TokenRx.Matches(codeLine);
            foreach (Match mm in m) list.Add(mm.Groups[1].Value);
            return list;
        }
    }
}
