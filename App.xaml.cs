// ============================================================================
//  ApolloGUI — App.xaml.cs
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
using System.Windows;
using ApolloGUI.Utilities;

namespace ApolloGUI
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Initialize global crash logging early
            CrashLogger.Init(this);

            try
            {
                var win = new MainWindow();
                win.Show();
            }
            catch (Exception ex)
            {
                // Log and fail gracefully if window creation dies
                CrashLogger.LogException("Startup(MainWindow ctor)", ex, isTerminating: true);
                MessageBox.Show($"Failed to start ApolloGUI.\n\nDetails logged to ApolloGUI_Crash.log.\n\n{ex.Message}",
                                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}
