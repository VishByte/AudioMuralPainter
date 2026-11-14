using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Amp
{
    internal sealed partial class SettingsForm : Form
    {
        readonly UiResources resources = new UiResources();
        readonly Preferences prefs;
        readonly Visualizer owner;
        readonly Color ink = Color.FromArgb(232, 239, 235), muted = Color.FromArgb(147, 164, 155), surface = Color.FromArgb(28, 35, 31), accent = Color.FromArgb(85, 255, 170);
        readonly FlowLayoutPanel body;
        readonly Label title, status;
        readonly Label shortcutHelp;
        readonly PreviewControl preview;
        readonly Timer statusTimer;
        readonly Button[] navigation = new Button[7];
        readonly List<Action> bindings = new List<Action>();
        readonly LinkLabel sectionReset;
        readonly string[] sections =
        {
            "Display",
            "Bars",
            "Oscilloscope",
            "Response",
            "Colors",
            "Labels",
            "Presets"
        };
        bool updating;
        int selected;
        CheckBox clickThroughCheck;
        HotkeyDialog hotkeyDialog;
        public SettingsForm(Visualizer owner, Preferences prefs)
        {
            SuspendLayout();
            this.owner = owner;
            this.prefs = prefs;
            Text = AppInfo.FullName + " Settings";
            Icon = owner.Icon;
            BackColor = Color.FromArgb(14, 19, 16);
            ForeColor = ink;
            Font = resources.GetFont("Segoe UI", 10);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(880, 740);
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            KeyPreview = true;
            MinimizeBox = false;
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                Padding = new Padding(22, 12, 22, 8)
            };
            header.Controls.Add(new Label { Text = "AMP  /  SETTINGS", ForeColor = accent, Dock = DockStyle.Top, Height = 24, Font = resources.GetFont("Segoe UI Semibold", 11) });
            header.Controls.Add(new Label { Text = "Changes are live. Click (default) beside any control to reset it.", ForeColor = muted, Dock = DockStyle.Bottom, Height = 24, Font = resources.GetFont("Segoe UI", 9) });
            var footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 96,
                Padding = new Padding(22, 8, 22, 10)
            };
            status = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                ForeColor = muted,
                Font = resources.GetFont("Segoe UI", 8)
            };
            footer.Controls.Add(status);
            shortcutHelp = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                ForeColor = muted,
                Font = resources.GetFont("Segoe UI", 8)
            };
            footer.Controls.Add(shortcutHelp);
            var main = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 18, 0)
            };
            var nav = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 156,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(14, 0, 8, 0)
            };
            for (int i = 0; i < sections.Length; i++)
            {
                int index = i;
                var button = CreateButton(sections[i]);
                button.Size = new Size(130, 38);
                button.Margin = new Padding(0, 0, 0, 5);
                button.Click += delegate
                {
                    BuildPage(index);
                };
                nav.Controls.Add(button);
                navigation[i] = button;
            }

            var resetAll = new LinkLabel
            {
                Text = "Reset visual settings",
                Size = new Size(130, 30),
                LinkColor = muted,
                ActiveLinkColor = accent,
                Margin = new Padding(0, 22, 0, 0),
                Font = resources.GetFont("Segoe UI", 9)
            };
            resetAll.LinkClicked += delegate
            {
                prefs.CopyFrom(new Preferences(), true);
                Changed();
                BuildPage(selected);
            };
            nav.Controls.Add(resetAll);
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 0, 0, 0)
            };
            var sectionHeading = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42
            };
            title = new Label
            {
                Dock = DockStyle.Fill,
                Font = resources.GetFont("Segoe UI", 18),
                ForeColor = ink
            };
            sectionReset = new LinkLabel
            {
                Text = "Reset section",
                Dock = DockStyle.Right,
                Width = 110,
                LinkColor = muted,
                ActiveLinkColor = accent,
                TextAlign = ContentAlignment.MiddleRight,
                Font = resources.GetFont("Segoe UI", 9)
            };
            sectionReset.LinkClicked += delegate
            {
                prefs.Reset(SectionFields(selected));
                Changed();
                BuildPage(selected);
            };
            sectionHeading.Controls.Add(title);
            sectionHeading.Controls.Add(sectionReset);
            var previewHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                Padding = new Padding(0, 0, 0, 12)
            };
            preview = new PreviewControl(prefs)
            {
                Dock = DockStyle.Fill
            };
            previewHost.Controls.Add(preview);
            body = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(0, 0, 2, 8)
            };
            body.SizeChanged += delegate
            {
                FitRows();
            };
            content.Controls.Add(body);
            content.Controls.Add(previewHost);
            content.Controls.Add(sectionHeading);
            main.Controls.Add(content);
            main.Controls.Add(nav);
            Controls.Add(main);
            Controls.Add(footer);
            Controls.Add(header);
            statusTimer = new Timer
            {
                Interval = 1000
            };
            statusTimer.Tick += delegate
            {
                status.Text = owner.SaveError ?? owner.AudioStatus;
            };
            statusTimer.Start();
            status.Text = owner.AudioStatus;
            RefreshShortcutHelp();
            BuildPage(0);
            ResumeLayout(true);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle work = Screen.FromControl(owner).WorkingArea;
            Size = new Size(Math.Min(Width, work.Width - 24), Math.Min(Height, work.Height - 24));
            Location = new Point(Math.Max(work.Left, Math.Min(owner.Left + (owner.Width - Width) / 2, work.Right - Width)), Math.Max(work.Top, Math.Min(owner.Top + (owner.Height - Height) / 2, work.Bottom - Height)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (statusTimer != null)
                    statusTimer.Dispose();
            }

            base.Dispose(disposing);
            if (disposing)
                resources.Dispose();
        }

        static string[] SectionFields(int page)
        {
            return new[]
            {
                Preferences.DisplayFields,
                Preferences.BarsFields,
                Preferences.ScopeFields,
                Preferences.ResponseFields,
                Preferences.ColorFields,
                Preferences.LabelFields,
                new string[0]
            }[page];
        }

        void FitRows()
        {
            int width = Math.Max(240, body.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 5);
            foreach (Control child in body.Controls)
                child.Width = width;
        }

        void Changed()
        {
            if (updating)
                return;
            prefs.Validate();
            owner.ApplyPreferences();
            updating = true;
            try
            {
                foreach (var binding in bindings)
                    binding();
            }
            finally
            {
                updating = false;
            }

            preview.RefreshSettings();
            RefreshShortcutHelp();
        }

        void RefreshShortcutHelp()
        {
            shortcutHelp.Text = "Settings: " + Shortcut.Display(prefs.Hotkeys.Settings) + " / Esc     Fullscreen: " + Shortcut.Display(prefs.Hotkeys.Fullscreen) + "     Quit: " + Shortcut.Display(prefs.Hotkeys.Quit) + Environment.NewLine + "Global settings / unlock: " + prefs.Hotkeys.GlobalSettings + "     •     Preferences save when settings close";
        }

        public void ShowHotkeys()
        {
            if (hotkeyDialog != null && !hotkeyDialog.IsDisposed)
            {
                hotkeyDialog.BringToFront();
                hotkeyDialog.Activate();
                return;
            }

            try
            {
                using (hotkeyDialog = new HotkeyDialog(owner, prefs.Hotkeys))
                    if (hotkeyDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        RefreshShortcutHelp();
                        if (selected == 0)
                            BuildPage(0);
                    }
            }
            finally
            {
                hotkeyDialog = null;
            }
        }

        public void ActivateSettings()
        {
            Form active = hotkeyDialog != null && !hotkeyDialog.IsDisposed ? (Form)hotkeyDialog : this;
            active.BringToFront();
            active.Activate();
        }

        public void RefreshInteractionState()
        {
            if (clickThroughCheck == null || clickThroughCheck.IsDisposed)
                return;
            bool prior = updating;
            updating = true;
            try
            {
                clickThroughCheck.Checked = prefs.ClickThrough;
                clickThroughCheck.Enabled = owner.CanEnableClickThrough;
            }
            finally
            {
                updating = prior;
            }
        }

        void BuildPage(int page)
        {
            selected = page;
            body.SuspendLayout();
            bindings.Clear();
            while (body.Controls.Count > 0)
            {
                var control = body.Controls[0];
                body.Controls.RemoveAt(0);
                control.Dispose();
            }

            title.Text = sections[page];
            sectionReset.Visible = page < 6;
            for (int i = 0; i < navigation.Length; i++)
            {
                navigation[i].BackColor = i == page ? Color.FromArgb(38, 67, 50) : surface;
                navigation[i].ForeColor = i == page ? accent : ink;
            }

            if (page == 0)
                DisplayPage();
            if (page == 1)
                BarsPage();
            if (page == 2)
                ScopePage();
            if (page == 3)
                ResponsePage();
            if (page == 4)
                ColorsPage();
            if (page == 5)
                LabelsPage();
            if (page == 6)
                PresetsPage();
            updating = true;
            try
            {
                foreach (var binding in bindings)
                    binding();
            }
            finally
            {
                updating = false;
            }

            FitRows();
            body.ResumeLayout(true);
            body.AutoScrollPosition = Point.Empty;
        }

        void Error(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "AMP", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (key == Keys.Escape)
            {
                Close();
                return true;
            }

            Control active = ActiveControl;
            while (active is ContainerControl && ((ContainerControl)active).ActiveControl != null)
                active = ((ContainerControl)active).ActiveControl;
            if (IsTextEditingKey(active, key))
                return base.ProcessCmdKey(ref msg, key);
            if (owner.DispatchShortcut(key))
                return true;
            return base.ProcessCmdKey(ref msg, key);
        }

        internal static bool IsTextEditingKey(Control control, Keys key)
        {
            if (!(control is TextBoxBase || control is UpDownBase || control is ComboBox) || (key & (Keys.Control | Keys.Alt)) != 0)
                return false;
            Keys code = key & Keys.KeyCode;
            return code == Keys.Back || (code >= Keys.Space && code <= Keys.Divide) || (code >= Keys.OemSemicolon && code <= Keys.OemBackslash);
        }
    }
}
