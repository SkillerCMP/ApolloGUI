// ============================================================================
//  ApolloGUI — MainWindow.CommandsInit.cs
//  Purpose: Command registration (Open, Reset DB, Export, etc.).
//  Key types: RoutedUICommand; bindings.
//  Notes: Single source of truth for gestures to avoid conflicts.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

// === MainWindow.CommandsInit.cs ===
// Ensures commands are bound and the Games UI is initialized on first render.

using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ApolloGUI  // <-- change to your namespace if different
{
    public partial class MainWindow : Window
    {
        private bool _initDone;

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (_initDone) return;
            _initDone = true;

            // Bind File menu commands (if not already bound)
            try
            {
                bool hasRefresh = false, hasAdd = false;

                // EXPLICITLY iterate CommandBinding, not object
                foreach (CommandBinding cb in this.CommandBindings)
                {
                    if (cb.Command == RefreshCommand) hasRefresh = true;
                    if (cb.Command == AddPatchCommand) hasAdd = true;
                }

                if (!hasRefresh)
                    CommandBindings.Add(new CommandBinding(RefreshCommand, Menu_Refresh_Execute, Menu_CanExecute_Always));
                if (!hasAdd)
                    CommandBindings.Add(new CommandBinding(AddPatchCommand, Menu_AddPatch_Execute, Menu_CanExecute_Always));
            }
            catch { }

            // Initialize Games UI
            try { LoadSortMode(); } catch { }
            try { SetupGamesCollectionSync(); } catch { }
            try { ApplyGamesSortAndFilter(); } catch { }
        }

        private void Menu_CanExecute_Always(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = true;

        private void Menu_Refresh_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try { RefreshDatabaseList(); }
            catch
            {
                try
                {
                    var mi = GetType().GetMethod("RefreshDatabaseList",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                    mi?.Invoke(this, null);
                }
                catch { }
            }
        }

        private void Menu_AddPatch_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var t = GetType();
                // Try BrowsePatch()
                var mi0 = t.GetMethod("BrowsePatch",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                if (mi0 != null) { mi0.Invoke(this, null); return; }

                // Try any BrowsePatch overload
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name == "BrowsePatch")
                    {
                        var pars = m.GetParameters();
                        var args = new object[pars.Length];
                        for (int i = 0; i < pars.Length; i++)
                            args[i] = pars[i].HasDefaultValue ? pars[i].DefaultValue
                                    : (pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null);
                        m.Invoke(this, args);
                        return;
                    }
                }

                // Fallback: legacy AddPatch_Click(object, RoutedEventArgs)
                var addMi = t.GetMethod("AddPatch_Click", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (addMi != null)
                {
                    var pars = addMi.GetParameters();
                    if (pars.Length == 2 && typeof(RoutedEventArgs).IsAssignableFrom(pars[1].ParameterType))
                        addMi.Invoke(this, new object[] { this, new RoutedEventArgs() });
                    else
                        addMi.Invoke(this, new object[pars.Length]);
                }
            }
            catch { }
        }
    }
}
