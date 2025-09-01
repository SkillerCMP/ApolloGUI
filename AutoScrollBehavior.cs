// ============================================================================
//  ApolloGUI — AutoScrollBehavior.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

// AutoScrollBehavior.cs
using System.Windows;
using System.Windows.Controls.Primitives; // TextBoxBase
using System.Windows.Threading;

namespace ApolloGUI // <-- change to your namespace
{
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        public static void SetAutoScroll(DependencyObject element, bool value) =>
            element.SetValue(AutoScrollProperty, value);

        public static bool GetAutoScroll(DependencyObject element) =>
            (bool)element.GetValue(AutoScrollProperty);

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBoxBase tb)
            {
                if ((bool)e.NewValue) tb.TextChanged += Tb_TextChanged;
                else tb.TextChanged -= Tb_TextChanged;
            }
        }

        private static void Tb_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is TextBoxBase tb)
            {
                tb.Dispatcher.BeginInvoke(DispatcherPriority.Background, new System.Action(() =>
                {
                    tb.ScrollToEnd();
                }));
            }
        }
    }
}
