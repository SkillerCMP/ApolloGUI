// Adds the missing double-click handler for the Collector list.
// Drop this file into your project. It compiles alongside your existing code.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApolloGUI
{
    public partial class CollectorWindow : Window
    {
        private void List_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var lb = sender as ListBox;
                if (lb?.SelectedItem is CollectorEntry item)
                {
                    // Toggle check on double-click, then refresh preview
                    item.IsChecked = !item.IsChecked;
                    UpdateCodePreviewFromSelection();
                }

                // prevent further bubbling to avoid unintended handlers firing
                e.Handled = true;
            }
            catch
            {
                // swallow to be crash-safe; optional: log if you have a logger
            }
        }
    }
}
