// ============================================================================
//  ApolloGUI — AmountPromptWindow.NumericGuards.cs
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
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApolloGUI
{
    public partial class AmountPromptWindow : Window
    {
        // Numeric ceiling for HEX entry
        private ulong? _maxNumeric;
        private string _lastValidNumeric = string.Empty;

        public void ConfigureNumericMax(ulong? max, bool fallbackTo32BitIfNull = true)
        {
            if (max.HasValue) _maxNumeric = max;
            else if (fallbackTo32BitIfNull) _maxNumeric = 0xFFFFFFFFUL;

            try
            {
                // seed last valid with current contents
                _lastValidNumeric = txtValue?.Text ?? string.Empty;
            }
            catch { }
        }

        private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_maxNumeric.HasValue) return;

            if (sender is not TextBox tb) { e.Handled = true; return; }

            int selStart = tb.SelectionStart;
            int selLen = tb.SelectionLength;
            string before = tb.Text.Substring(0, selStart);
            string after  = tb.Text.Substring(selStart + selLen);
            string proposed = (before + e.Text + after).Trim();

            if (proposed.Length == 0) return;
            if (!proposed.All(char.IsDigit)) { e.Handled = true; return; }
            if (!ulong.TryParse(proposed, NumberStyles.None, CultureInfo.InvariantCulture, out var v)) { e.Handled = true; return; }
            if (v > _maxNumeric.Value) { e.Handled = true; return; }
        }

        private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_maxNumeric.HasValue) return;
            if (sender is not TextBox tb) return;

            string sVal = tb.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(sVal))
            {
                _lastValidNumeric = string.Empty;
                return;
            }

            if (!sVal.All(char.IsDigit) ||
                !ulong.TryParse(sVal, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ||
                v > _maxNumeric.Value)
            {
                int caret = tb.CaretIndex;
                tb.Text = _lastValidNumeric;
                tb.CaretIndex = Math.Max(0, Math.Min(caret - 1, tb.Text.Length));
                return;
            }

            _lastValidNumeric = sVal;
        }
    }
}
