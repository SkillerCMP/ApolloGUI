// ============================================================================
//  ApolloGUI — MainWindow.Hotkeys.cs
//  Purpose: Hotkey setup and helpers.
//  Key types: KeyGesture; InputBinding.
//  Notes: Document OS-level conflicts.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

// Minimal Hotkeys file: ONLY declares the commands.
// Handlers & bindings are provided in MainWindow.CommandsInit.cs.

using System.Windows;
using System.Windows.Input;

namespace ApolloGUI  // <-- change to your namespace if different
{
    public partial class MainWindow : Window
    {
        public static readonly RoutedUICommand RefreshCommand =
            new RoutedUICommand("Refresh", "RefreshCommand", typeof(MainWindow));

        public static readonly RoutedUICommand AddPatchCommand =
            new RoutedUICommand("Add Patch…", "AddPatchCommand", typeof(MainWindow));
    }
}