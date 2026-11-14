using System;
using System.Linq.Expressions;
using System.Drawing;
using System.Windows.Forms;

namespace Amp
{
    internal sealed partial class SettingsForm
    {
        static PreferenceBinding Field(Expression<Func<Preferences, object>> selector)
        {
            return PreferenceBinding.For(selector);
        }

        object Get(PreferenceBinding field)
        {
            return field.Get(prefs);
        }

        void Set(PreferenceBinding field, object value)
        {
            field.Set(prefs, value);
        }

        Button CreateButton(string text)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = surface,
                ForeColor = ink,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand,
                Height = 34
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        void Row(string label, Control editor, PreferenceBinding[] fields, int height)
        {
            var row = new TableLayoutPanel
            {
                Height = height,
                Margin = new Padding(0, 0, 0, 3),
                ColumnCount = 3,
                RowCount = 1
            };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 41));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            var caption = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ink,
                Font = resources.GetFont("Segoe UI", 9),
                Margin = new Padding(0, 0, 6, 0)
            };
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(0, 6, 5, 5);
            var reset = new LinkLabel
            {
                Text = "(default)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                LinkColor = muted,
                ActiveLinkColor = accent,
                Font = resources.GetFont("Segoe UI", 8),
                AccessibleName = "Reset " + label + " to default",
                Margin = new Padding(0)
            };
            reset.LinkClicked += delegate
            {
                foreach (var field in fields)
                    field.Reset(prefs);
                Changed();
            };
            row.Controls.Add(caption, 0, 0);
            row.Controls.Add(editor, 1, 0);
            row.Controls.Add(reset, 2, 0);
            body.Controls.Add(row);
        }

        void Heading(string text)
        {
            body.Controls.Add(new Label { Text = text, Height = 31, ForeColor = accent, Font = resources.GetFont("Segoe UI Semibold", 10), TextAlign = ContentAlignment.BottomLeft, Margin = new Padding(0, 4, 0, 5) });
        }

        void Info(string text, int height)
        {
            body.Controls.Add(new Label { Text = text, Height = height, ForeColor = muted, Font = resources.GetFont("Segoe UI", 8.5f), Margin = new Padding(0, 0, 0, 8) });
        }

        void Choice(string label, PreferenceBinding field, string[] options, Action after)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = surface,
                ForeColor = ink,
                AccessibleName = label
            };
            combo.Items.AddRange(options);
            combo.SelectedItem = Get(field).ToString();
            bindings.Add(delegate
            {
                combo.SelectedItem = Get(field).ToString();
            });
            combo.SelectedIndexChanged += delegate
            {
                if (updating)
                    return;
                Set(field, combo.SelectedItem);
                if (after != null)
                    after();
                Changed();
            };
            Row(label, combo, new[] { field }, 40);
        }

        void Choice(string label, PreferenceBinding field, params string[] options)
        {
            Choice(label, field, options, null);
        }

        CheckBox Check(string label, PreferenceBinding field, Func<bool> enabled, Action after)
        {
            var box = new CheckBox
            {
                Text = "Enabled",
                Checked = (bool)Get(field),
                ForeColor = ink,
                AccessibleName = label
            };
            bindings.Add(delegate
            {
                box.Checked = (bool)Get(field);
                box.Enabled = enabled == null || enabled();
            });
            box.CheckedChanged += delegate
            {
                if (updating)
                    return;
                Set(field, box.Checked);
                if (after != null)
                    after();
                Changed();
            };
            Row(label, box, new[] { field }, 39);
            return box;
        }

        void Check(string label, PreferenceBinding field)
        {
            Check(label, field, null, null);
        }

        NumericUpDown Number(string label, PreferenceBinding field, decimal min, decimal max, int decimals, decimal increment, Func<bool> enabled)
        {
            return Numeric(label, () => Convert.ToDecimal(Get(field)), v => Set(field, v), () => min, () => max, decimals, increment, new[] { field }, enabled);
        }

        NumericUpDown Number(string label, PreferenceBinding field, decimal min, decimal max)
        {
            return Number(label, field, min, max, 0, 1, null);
        }

        NumericUpDown Numeric(string label, Func<decimal> get, Action<decimal> set, Func<decimal> min, Func<decimal> max, int decimals, decimal increment, PreferenceBinding[] fields, Func<bool> enabled)
        {
            var number = new NumericUpDown
            {
                Minimum = min(),
                Maximum = max(),
                DecimalPlaces = decimals,
                Increment = increment,
                BackColor = surface,
                ForeColor = ink,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Right,
                AccessibleName = label
            };
            Action refresh = delegate
            {
                number.Minimum = min();
                number.Maximum = max();
                number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, get()));
                number.Enabled = enabled == null || enabled();
            };
            refresh();
            bindings.Add(refresh);
            number.ValueChanged += delegate
            {
                if (updating)
                    return;
                set(number.Value);
                Changed();
            };
            Row(label, number, fields, 40);
            return number;
        }

        void Slider(string label, PreferenceBinding field, int min, int max)
        {
            var group = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1
            };
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            var slider = new TrackBar
            {
                Minimum = min,
                Maximum = max,
                TickStyle = TickStyle.None,
                Dock = DockStyle.Fill,
                AutoSize = false,
                AccessibleName = label,
                Margin = new Padding(0)
            };
            var number = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Dock = DockStyle.Fill,
                BackColor = surface,
                ForeColor = ink,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(6, 1, 0, 0),
                AccessibleName = label + " value"
            };
            Action refresh = delegate
            {
                slider.Value = Convert.ToInt32(Get(field));
                number.Value = slider.Value;
            };
            refresh();
            bindings.Add(refresh);
            slider.ValueChanged += delegate
            {
                if (updating)
                    return;
                Set(field, slider.Value);
                Changed();
            };
            number.ValueChanged += delegate
            {
                if (updating)
                    return;
                Set(field, number.Value);
                Changed();
            };
            group.Controls.Add(slider, 0, 0);
            group.Controls.Add(number, 1, 0);
            Row(label, group, new[] { field }, 46);
        }

        void ColorRow(string label, PreferenceBinding field)
        {
            var group = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1
            };
            group.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var swatch = CreateButton("");
            swatch.Dock = DockStyle.Fill;
            swatch.Margin = new Padding(0, 0, 8, 0);
            swatch.AccessibleName = label + " color picker";
            var input = new TextBox
            {
                Text = "#" + Get(field),
                BackColor = surface,
                ForeColor = ink,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 7,
                Dock = DockStyle.Fill,
                AccessibleName = label + " hex color",
                Margin = new Padding(0, 1, 0, 0)
            };
            Action refresh = delegate
            {
                swatch.BackColor = Preferences.ColorOf((string)Get(field));
                if (!input.Focused)
                    input.Text = "#" + Get(field);
            };
            refresh();
            bindings.Add(refresh);
            input.TextChanged += delegate
            {
                if (updating)
                    return;
                Color c;
                if (Preferences.TryColor(input.Text, out c))
                {
                    Set(field, Preferences.Hex(c));
                    input.ForeColor = ink;
                    Changed();
                }
                else
                    input.ForeColor = Color.FromArgb(255, 115, 115);
            };
            input.Leave += delegate
            {
                input.Text = "#" + Get(field);
                input.ForeColor = ink;
            };
            swatch.Click += delegate
            {
                using (var dialog = new ColorDialog
                {
                    Color = Preferences.ColorOf((string)Get(field)),
                    FullOpen = true
                }

                )
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        Set(field, Preferences.Hex(dialog.Color));
                        input.Text = "#" + Get(field);
                        Changed();
                    }
            };
            group.Controls.Add(swatch, 0, 0);
            group.Controls.Add(input, 1, 0);
            Row(label, group, new[] { field }, 43);
        }
    }
}
