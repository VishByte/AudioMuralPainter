using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Amp
{
    internal sealed partial class SettingsForm
    {
        void DisplayPage()
        {
            Choice("Visualizer", Field(p => p.View), "Both", "Oscilloscope", "Bars");
            Slider("Background opacity (%)", Field(p => p.BackgroundOpacity), 0, 100);
            Info("0 = clear, 100 = black. Visuals keep their brightness. Mouse interaction is controlled separately below.", 36);
            clickThroughCheck = Check("Mouse click-through", Field(p => p.ClickThrough), () => owner.CanEnableClickThrough, null);
            Info("Global settings / unlock: " + prefs.Hotkeys.GlobalSettings + ". Opening settings pauses click-through. Right-click the AMP tray icon for Open settings or Make AMP clickable.", 48);
            var hotkeys = CreateButton("Customize hotkeys…");
            hotkeys.Margin = new Padding(0, 2, 0, 10);
            hotkeys.Click += delegate
            {
                ShowHotkeys();
            };
            body.Controls.Add(hotkeys);
            if (!owner.CanEnableClickThrough)
                Info("Click-through becomes available when Windows reserves the global recovery shortcut. Use Customize hotkeys if the shortcut is in use.", 44);
            var dimensions = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = surface,
                ForeColor = ink,
                FlatStyle = FlatStyle.Flat,
                AccessibleName = "Window size"
            };
            dimensions.Items.AddRange(new object[] { "Custom / current", "800 × 480", "1000 × 600", "1280 × 720", "1920 × 1080" });
            dimensions.SelectedIndex = 0;
            dimensions.SelectedIndexChanged += delegate
            {
                if (dimensions.SelectedIndex == 0)
                    return;
                string[] values = dimensions.SelectedItem.ToString().Split('×');
                owner.SetWindowSize(int.Parse(values[0].Trim()), int.Parse(values[1].Trim()));
            };
            // Window geometry is deliberately excluded from appearance presets.
            var sizeRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                Height = 40,
                Margin = new Padding(0, 0, 0, 3)
            };
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            sizeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            sizeRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sizeRow.Controls.Add(new Label { Text = "Window size", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = resources.GetFont("Segoe UI", 9) }, 0, 0);
            var sizeGroup = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                Dock = DockStyle.Fill
            };
            sizeGroup.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sizeGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
            sizeGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            dimensions.Dock = DockStyle.Fill;
            var sizeReset = new LinkLabel
            {
                Text = "(default)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                LinkColor = muted,
                Font = resources.GetFont("Segoe UI", 8)
            };
            sizeReset.LinkClicked += delegate
            {
                owner.SetWindowSize(1000, 600);
                dimensions.SelectedIndex = 0;
            };
            sizeGroup.Controls.Add(dimensions, 0, 0);
            sizeGroup.Controls.Add(sizeReset, 1, 0);
            sizeRow.Controls.Add(sizeGroup, 1, 0);
            body.Controls.Add(sizeRow);
            Choice("Maximum refresh rate (fps)", Field(p => p.Fps), new[] { "30", "60", "120" }, null);
            Number("Outer margin (logical px)", Field(p => p.Padding), 0, 64);
            Number("Gap between views (px)", Field(p => p.ViewGap), 0, 64);
            Number("Scope share of both views (%)", Field(p => p.ScopeShare), 15, 85);
            Check("Always on top", Field(p => p.OnTop));
            Info("Margins and view gap stay the same size as the window grows. Windows display scaling is respected.", 36);
        }

        void BarsPage()
        {
            Heading("Frequency range");
            Number("Minimum frequency (Hz)", Field(p => p.MinFrequency), 1, 95999, 1, 10, null);
            Number("Maximum frequency (Hz)", Field(p => p.MaxFrequency), 2, 96000, 1, 100, null);
            Choice("Frequency spacing", Field(p => p.FrequencyScale), "Logarithmic", "Linear");
            Number("Number of bars", Field(p => p.Bars), 4, 256);
            Choice("Frequency resolution (FFT)", Field(p => p.FftSize), new[] { "1024", "2048", "4096", "8192", "16384", "32768" }, null);
            Info("Labels follow the effective range. Frequencies are limited to what the playback device can represent. Larger FFT sizes improve bass detail with more latency.", 48);
            Heading("Level range and cutoff");
            Number("Meter floor (dBFS)", Field(p => p.MeterFloorDb), -120, -12);
            Check("Minimum response cutoff", Field(p => p.GateEnabled));
            Number("Cutoff level (dBFS)", Field(p => p.GateDb), -120, 0, 0, 1, () => prefs.GateEnabled);
            Number("Soft cutoff width (dB)", Field(p => p.GateSoftnessDb), 0, 24, 0, 1, () => prefs.GateEnabled);
            Info("Cutoff uses the incoming level before sensitivity gain. 0 dB softness gives a hard cutoff; a wider edge fades quiet responses. Existing bars settle using release.", 48);
            Heading("Shape");
            Check("Segmented bars", Field(p => p.Segmented));
            Number("Space between bars (px)", Field(p => p.BarGap), 0, 32, 1, .5m, null);
            Number("Segment height (px)", Field(p => p.SegmentHeight), 1, 32, 1, .5m, () => prefs.Segmented);
            Number("Segment gap (px)", Field(p => p.SegmentGap), 0, 16, 1, .5m, () => prefs.Segmented);
            Check("Dim baseline segments", Field(p => p.IdleBars));
            Number("Baseline opacity (%)", Field(p => p.IdleOpacity), 0, 100, 0, 1, () => prefs.IdleBars);
            Check("Peak markers", Field(p => p.Peaks));
        }

        void ScopePage()
        {
            Number("Waveform detail (points)", Field(p => p.WavePoints), 128, 8192);
            Number("Time window (ms)", Field(p => p.TimeMs), 1, 200);
            Number("Line thickness (px)", Field(p => p.WaveThickness), .5m, 8, 1, .1m, null);
            Slider("Glow strength (%)", Field(p => p.WaveGlow), 0, 100);
            Info("A shorter time window zooms in on the wave. Detail controls drawing points. Time labels and the amplitude scale have separate toggles on Labels.", 48);
        }

        void ResponsePage()
        {
            Check("Link both sensitivities", Field(p => p.LinkSensitivity), null, delegate
            {
                if (!prefs.LinkSensitivity)
                {
                    prefs.ScopeGain = prefs.Gain;
                    prefs.BarGain = prefs.Gain;
                }
            });
            Number("Linked sensitivity (dB)", Field(p => p.Gain), -18, 36, 0, 1, () => prefs.LinkSensitivity);
            Number("Scope sensitivity (dB)", Field(p => p.ScopeGain), -18, 36, 0, 1, () => !prefs.LinkSensitivity);
            Number("Bars sensitivity (dB)", Field(p => p.BarGain), -18, 36, 0, 1, () => !prefs.LinkSensitivity);
            Heading("Bar movement");
            Number("Attack (ms)", Field(p => p.AttackMs), 1, 500);
            Number("Release / smoothing (ms)", Field(p => p.ReleaseMs), 10, 3000);
            Info("Short attack catches transients. Long release produces a smoother fall. These timings stay consistent at different refresh rates.", 38);
            Number("Peak hold (ms)", Field(p => p.PeakHoldMs), 0, 3000, 0, 10, () => prefs.Peaks);
            Number("Peak full-scale fall time (ms)", Field(p => p.PeakFallMs), 100, 10000, 0, 100, () => prefs.Peaks);
        }

        void ColorsPage()
        {
            Heading("Level colors and thresholds");
            ColorRow("Bars / low", Field(p => p.LowColor));
            ColorRow("Bars / middle", Field(p => p.MidColor));
            ColorRow("Bars / peak", Field(p => p.HighColor));
            Choice("Threshold units", Field(p => p.ThresholdUnit), new[] { "Percent", "dBFS" }, delegate
            {
                BeginInvoke(new Action(delegate
                {
                    if (!IsDisposed && selected == 4)
                        BuildPage(4);
                }));
            });
            Func<double, decimal> value = pct => (decimal)(prefs.ThresholdUnit == "Percent" ? pct : prefs.ThresholdDb(pct));
            Action<bool, decimal> set = delegate (bool middle, decimal input)
            {
                double pct = prefs.ThresholdUnit == "Percent" ? (double)input : prefs.ThresholdPercent((double)input);
                if (middle)
                {
                    prefs.MidThreshold = pct;
                    if (prefs.HighThreshold <= pct)
                        prefs.HighThreshold = Math.Min(99, pct + 1);
                }
                else
                {
                    prefs.HighThreshold = pct;
                    if (prefs.MidThreshold >= pct)
                        prefs.MidThreshold = Math.Max(1, pct - 1);
                }
            };
            Numeric("Middle color begins", () => value(prefs.MidThreshold), v => set(true, v), () => value(1), () => value(98), 1, 1, new[] { Field(p => p.MidThreshold) }, null);
            Numeric("Peak color begins", () => value(prefs.HighThreshold), v => set(false, v), () => value(2), () => value(99), 1, 1, new[] { Field(p => p.HighThreshold) }, null);
            var thresholdPreview = new ThresholdPreview(prefs)
            {
                Height = 57,
                Margin = new Padding(0, 5, 0, 8)
            };
            thresholdPreview.Changed += delegate
            {
                Changed();
            };
            body.Controls.Add(thresholdPreview);
            bindings.Add(delegate
            {
                thresholdPreview.Invalidate();
            });
            Info("Drag the two markers or enter values. Percent and dBFS are two views of the same thresholds. dBFS here is the displayed level after sensitivity gain.", 45);
            Heading("Gradients");
            Choice("Bar color style", Field(p => p.BarStyle), "Level colors", "Gradient");
            Choice("Scope color style", Field(p => p.ScopeStyle), "Solid", "Gradient");
            Choice("Gradient direction", Field(p => p.GradientDirection), "Vertical", "Horizontal");
            Check("Reverse gradient direction", Field(p => p.GradientReverse));
            Choice("Gradient coverage", Field(p => p.GradientCoverage), "Each bar", "Bars view", "Both views");
            Info("Vertical runs bottom → top; reverse runs top → bottom. Horizontal runs left → right. Both views uses the bar palette continuously across scope, gap, and bars; enable Gradient for both styles.", 58);
            Heading("Oscilloscope palette");
            ColorRow("Scope / solid or gradient start", Field(p => p.ScopeColor));
            ColorRow("Scope / gradient middle", Field(p => p.ScopeGradientMid));
            ColorRow("Scope / gradient end", Field(p => p.ScopeGradientEnd));
        }

        void LabelsPage()
        {
            Heading("Independent toggles");
            Check("Bars frequency labels", Field(p => p.BarsLabels));
            Check("Scope time labels", Field(p => p.ScopeTimeLabels));
            Check("Scope amplitude scale", Field(p => p.ScopeAmplitudeLabels));
            Check("Bars guide grid", Field(p => p.BarsGrid));
            Check("Scope guide grid", Field(p => p.ScopeGrid));
            Heading("Appearance");
            ColorRow("Label / grid color", Field(p => p.LabelColor));
            Number("Label size (px)", Field(p => p.LabelSize), 6, 24);
            Slider("Label opacity (%)", Field(p => p.LabelOpacity), 0, 100);
            Number("Maximum horizontal labels", Field(p => p.LabelDensity), 2, 16);
            Slider("Grid opacity (%)", Field(p => p.GridOpacity), 0, 100);
            Info("Labels adapt to the available width. Scope time runs from the start of the displayed window to 0 ms; amplitude is normalized from −1 to +1 after gain.", 46);
        }

        void PresetsPage()
        {
            Info("Presets include appearance and response settings. Applying a preset keeps your current window size and position.", 40);
            var picker = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = surface,
                ForeColor = ink,
                FlatStyle = FlatStyle.Flat,
                Height = 32,
                Margin = new Padding(0, 2, 0, 8),
                AccessibleName = "Preset"
            };
            picker.Items.AddRange(new object[] { "Default", "Minimal scope", "Classic meters", "Continuous gradient", "Glass" });
            foreach (string name in Presets.Names())
                picker.Items.Add("Saved: " + name);
            picker.SelectedIndex = 0;
            body.Controls.Add(picker);
            var apply = CreateButton("Apply selected preset");
            apply.Margin = new Padding(0, 0, 0, 16);
            apply.Click += delegate
            {
                try
                {
                    string name = picker.SelectedItem.ToString();
                    prefs.CopyFrom(name.StartsWith("Saved: ") ? Preferences.Read(Presets.PathFor(name.Substring(7))) : Presets.BuiltIn(name), true);
                    Changed();
                    status.Text = "Applied " + name;
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            };
            body.Controls.Add(apply);
            Heading("Save your current setup");
            var nameInput = new TextBox
            {
                BackColor = surface,
                ForeColor = ink,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 48,
                Text = "My preset",
                Height = 28,
                Margin = new Padding(0, 3, 0, 8),
                AccessibleName = "New preset name"
            };
            body.Controls.Add(nameInput);
            var save = CreateButton("Save named preset");
            save.Click += delegate
            {
                try
                {
                    string name = nameInput.Text.Trim();
                    string path = Presets.PathFor(name);
                    if (File.Exists(path) && MessageBox.Show(this, "Replace the saved preset “" + name + "”?", "AMP", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    Presets.Save(name, prefs);
                    BuildPage(6);
                    status.Text = "Saved " + name;
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
            };
            body.Controls.Add(save);
            Heading("Share or back up");
            var import = CreateButton("Import preset…");
            import.Margin = new Padding(0, 4, 0, 8);
            import.Click += delegate
            {
                using (var dialog = new OpenFileDialog
                {
                    Filter = "AMP presets (*.xml)|*.xml",
                    Title = "Import AMP preset"
                }

                )
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        try
                        {
                            prefs.CopyFrom(Preferences.Read(dialog.FileName), true);
                            Changed();
                            status.Text = "Imported preset. Use Save named preset to keep it in your library.";
                        }
                        catch (Exception ex)
                        {
                            Error(ex);
                        }
            };
            body.Controls.Add(import);
            var export = CreateButton("Export current preset…");
            export.Click += delegate
            {
                using (var dialog = new SaveFileDialog
                {
                    Filter = "AMP presets (*.xml)|*.xml",
                    DefaultExt = "xml",
                    FileName = "AMP preset.xml",
                    Title = "Export AMP preset"
                }

                )
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        try
                        {
                            prefs.WritePreset(dialog.FileName);
                            status.Text = "Preset exported.";
                        }
                        catch (Exception ex)
                        {
                            Error(ex);
                        }
            };
            body.Controls.Add(export);
        }
    }
}
