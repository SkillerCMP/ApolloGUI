// ============================================================================
//  ApolloGUI — PatchItem.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

// Auto-added by check script to satisfy MainWindow.GamesSort.cs
namespace ApolloGUI
{
    public class PatchItem
    {
        public string? Display { get; set; }
        public string? Name { get; set; }
        public string? Id { get; set; }
        public override string ToString() => Display ?? base.ToString();
    }
}
