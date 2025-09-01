// ============================================================================
//  ApolloGUI — AmountPromptWindow.xaml.cs
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
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ApolloGUI
{
    public partial class AmountPromptWindow : Window
    {

            private static byte[] BuildBytesForValue(ulong value, int widthBytes)
            {
                if (widthBytes < 1) widthBytes = 1;
                if (widthBytes > 8) widthBytes = 8;
                var le = BitConverter.GetBytes(value); // runtime little-endian
                var slice = new byte[widthBytes];
                Array.Copy(le, 0, slice, 0, widthBytes);
                return slice; // NO reverse here
            }
        

        private int? _maxLen;

        public string? SelectedType => (cmbType.SelectedItem as ComboBoxItem)?.Content?.ToString();
        public string? SelectedEndian => (cmbEndian.SelectedItem as ComboBoxItem)?.Content?.ToString();
        public string? ResultValue => txtValue.Text;

        public AmountPromptWindow(string? type, string? endian, string? initialDisplay, string? originalDisplay, bool allowTypeAndEndianSelection = true)
        {
            InitializeComponent();

            lblOriginal.Text = originalDisplay ?? string.Empty;

            if (!allowTypeAndEndianSelection)
            {
                cmbType.Items.Clear();
                cmbType.Items.Add(new ComboBoxItem { Content = (type ?? "").ToUpperInvariant() });
                cmbType.SelectedIndex = 0;
                cmbType.IsEnabled = false;

                cmbEndian.Items.Clear();
                cmbEndian.Items.Add(new ComboBoxItem { Content = (endian ?? "").ToUpperInvariant() });
                cmbEndian.SelectedIndex = 0;
                cmbEndian.IsEnabled = false;
            }
            else
            {
                cmbType.SelectedIndex = (type ?? "HEX").ToUpperInvariant() == "FLOAT" ? 1 : 0;
                var e = (endian ?? "BIG").ToUpperInvariant();
                cmbEndian.SelectedIndex = e == "LITTLE" ? 1 : (e == "TXT" ? 2 : 0);
                cmbType.IsEnabled = true;
                cmbEndian.IsEnabled = true;
            }

            txtValue.Text = initialDisplay ?? string.Empty;
            txtValue.SelectAll();
            txtValue.TextChanged += (_, __) => UpdateCounter();
            UpdateCounter();
        }

        // Called by code-behind before ShowDialog() to enforce inline length
        public void ConfigureMaxLength(int? maxLen)
        {
            _maxLen = (maxLen.HasValue && maxLen.Value > 0) ? maxLen : null;
            txtValue.MaxLength = _maxLen ?? int.MaxValue;
            UpdateCounter();
            if (_maxLen.HasValue)
            {
                txtValue.ToolTip = $"Maximum characters: {_maxLen.Value}";
            }
            else
            {
                txtValue.ClearValue(TextBox.ToolTipProperty);
            }
        }

        private void UpdateCounter()
        {
            var len = txtValue.Text?.Length ?? 0;
            if (_maxLen.HasValue)
            {
                lblCounter.Text = $"{len}/{_maxLen.Value}";
                // With MaxLength set, len should never exceed, but guard regardless
                btnOK.IsEnabled = len <= _maxLen.Value;
            }
            else
            {
                lblCounter.Text = $"{len}/∞";
                btnOK.IsEnabled = true;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
