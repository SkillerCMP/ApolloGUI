// ============================================================================
//  ApolloGUI — SavepatchText.cs
//  Purpose: Parse .savepatch text: headers, IDs, names, code groups, metadata.
//  Key types: Tokenizers; models for Game/Code/Mod.
//  Notes: Support first ';' as ID, second ';' as Game Name; multiple Hash/GameID headers.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ApolloGUI
{
    public class SavepatchText
    {
        public string? Cusa { get; set; }
        public string? Title { get; set; }
        public string? Platform { get; set; }
        public string? Source { get; set; }
        public List<string> Metadata { get; set; } = new List<string>();
        public List<CodeBlock> Codes { get; set; } = new List<CodeBlock>();

        public class CodeBlock
        {
            public string Name { get; set; } = "";
            public int Index { get; set; }
            public List<string> Lines { get; set; } = new List<string>();
        }

        public static SavepatchText Load(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static SavepatchText Parse(string content)
        {
            var sp = new SavepatchText();
            var lines = content.Replace("\r", "").Split('\n');

            // ===== 1) Pre-scan: first two ';' lines =====
            var headerSemis = new List<string>(2);
            bool headerStarted = false;
            foreach (var raw in lines)
            {
                var t = (raw ?? "").TrimEnd();
                if (t.Length == 0)
                {
                    if (headerStarted) break; // stop header once it started and we hit blank
                    continue;
                }

                if (t.StartsWith(";"))
                {
                    headerStarted = true;
                    var v = t.Substring(1).Trim(); // strip leading ';' and trim spaces
                    if (v.Length > 0) headerSemis.Add(v);
                    if (headerSemis.Count >= 2) break;
                }
                else
                {
                    if (headerStarted) break;
                }
            }

            // Map 1st ';' -> CUSA/ID, 2nd ';' -> Title (optionally "PS4 <name>")
            if (headerSemis.Count >= 1)
            {
                var id = headerSemis[0];
                var m = Regex.Match(id, @"^CUSA(\d+)$", RegexOptions.IgnoreCase);
                sp.Cusa = m.Success ? ("CUSA" + m.Groups[1].Value) : id;
            }
            if (headerSemis.Count >= 2)
            {
                var name = headerSemis[1];
                var mPs4 = Regex.Match(name, @"^(?i:PS4)\s*(.*)$");
                if (mPs4.Success)
                {
                    sp.Platform = "PS4";
                    sp.Title = (mPs4.Groups[1].Value ?? "").Trim();
                }
                else
                {
                    sp.Title = name;
                }
            }

            // ===== 2) Existing parse for the rest (metadata, code blocks, ;source:) =====
            var headerSource = new Regex(@"^;source:\s*(.*)$", RegexOptions.IgnoreCase);

            SavepatchText.CodeBlock? current = null;
            int idx = 0;

            foreach (var raw in lines)
            {
                var line = (raw ?? "").TrimEnd();
                if (line.Length == 0) continue;

                if (line.StartsWith(";"))
                {
                    // capture ;source:
                    var mSrc = headerSource.Match(line);
                    if (mSrc.Success) sp.Source = mSrc.Groups[1].Value.Trim();
                    continue;
                }

                if (line.StartsWith(":*") || line.StartsWith(": "))
                {
                    sp.Metadata.Add(line);
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    current = new SavepatchText.CodeBlock { Name = line.Substring(1, line.Length - 2).Trim(), Index = ++idx };
                    sp.Codes.Add(current);
                    continue;
                }

                if (current != null) current.Lines.Add(line);
                else sp.Metadata.Add(line);
            }

            return sp;
        }
    }
}