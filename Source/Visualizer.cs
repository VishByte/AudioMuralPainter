using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Amp
{
    internal sealed class Visualizer : Form
    {
        const int NonClientHitTest = 0x0084;
        const int NonClientDoubleClick = 0x00A3;
        enum HitTest
        {
            Client = 1,
            Caption = 2,
            Left = 10,
            Right = 11,
            Top = 12,
            TopLeft = 13,
            TopRight = 14,
            Bottom = 15,
            BottomLeft = 16,
            BottomRight = 17
        }

        readonly Preferences prefs;
        readonly Loopback audio;
        readonly SceneRenderer renderer = new SceneRenderer();
        readonly LayeredSurface surface = new LayeredSurface();
        readonly float[] samples = new float[65536];
        readonly Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        double previousTime;
        double lastSignalTime = double.NegativeInfinity;
        bool hasDrawn;
        bool restFrameDrawn;
        int lastRenderedRate;
        bool layered, fullscreen, closing, rendering;
        Rectangle windowedBounds;
        SettingsForm settings;
        DesktopIntegration desktop;
        bool desktopStarted;
        bool resourcesDisposed;
        Icon ownedIcon;
        string renderError;
        public string SaveError { get; private set; }

        public string AudioStatus
        {
            get
            {
                return desktop != null && desktop.RecoveryError != null ? desktop.RecoveryError : renderError ?? audio.Status;
            }
        }

        public bool CanEnableClickThrough
        {
            get
            {
                return desktop != null && desktop.RecoveryAvailable;
            }
        }

        public bool ClickThroughActive
        {
            get
            {
                return desktop != null && desktop.EffectiveClickThrough;
            }
        }

        public Visualizer(Preferences prefs) : this(prefs, true)
        {
        }

        internal Visualizer(Preferences prefs, bool captureAudio)
        {
            if (prefs == null)
                throw new ArgumentNullException("prefs");
            prefs.Validate();
            this.prefs = prefs;
            Text = AppInfo.FullName;
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(400, 240);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            StartPosition = FormStartPosition.Manual;
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Size size = new Size(Math.Min(prefs.Width, work.Width), Math.Min(prefs.Height, work.Height));
            Rectangle saved = new Rectangle(prefs.Left, prefs.Top, size.Width, size.Height);
            bool usable = false;
            foreach (var monitor in Screen.AllScreens)
            {
                var visible = Rectangle.Intersect(monitor.WorkingArea, saved);
                if (visible.Width >= 160 && visible.Height >= 120)
                    usable = true;
            }

            Bounds = usable ? saved : new Rectangle(work.Left + (work.Width - size.Width) / 2, work.Top + (work.Height - size.Height) / 2, size.Width, size.Height);
            try
            {
                ownedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                Icon = ownedIcon;
            }
            catch
            {
            }

            audio = new Loopback(captureAudio);
            timer = new Timer();
            timer.Tick += delegate
            {
                if (WindowState != FormWindowState.Minimized)
                    RenderFrame();
            };
            ApplyPreferences();
            Shown += delegate
            {
                desktopStarted = true;
                StartDesktop();
                ConfigureLayering();
                RenderFrame();
                timer.Start();
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel)
                return;
            closing = true;
            timer.Stop();
            if (settings != null && !settings.IsDisposed)
                settings.Close();
            SavePreferences();
        }

        void ConfigureLayering()
        {
            bool through = ClickThroughActive, next = InteractionPolicy.Layered(prefs.BackgroundOpacity, through);
            if (!IsHandleCreated)
                return;
            LayeredSurface.SetWindowMode(Handle, next, through);
            if (next == layered)
                return;
            layered = next;
            hasDrawn = false;
            if (!next)
            {
                surface.Dispose();
                Invalidate();
            }
        }

        void StartDesktop()
        {
            if (closing || desktop != null)
                return;
            desktop = new DesktopIntegration(this, prefs, ShowSettings, CustomizeHotkeys, Close, ApplyInteraction, SavePreferences);
            if (settings != null && !settings.IsDisposed)
                desktop.SetSettingsOpen(true);
        }

        void ApplyInteraction()
        {
            if (closing)
                return;
            hasDrawn = false;
            ConfigureLayering();
            RenderFrame();
            if (settings != null)
                settings.RefreshInteractionState();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (desktopStarted)
            {
                // WinForms finishes applying its native styles after this callback.
                // Restore per-pixel layering only once handle recreation is complete.
                BeginInvoke(new Action(delegate
                {
                    if (closing || IsDisposed || !IsHandleCreated)
                        return;
                    LayeredSurface.SetWindowMode(Handle, false, false);
                    layered = false;
                    StartDesktop();
                    hasDrawn = false;
                    ConfigureLayering();
                    RenderFrame();
                }));
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (desktop != null)
            {
                desktop.Dispose();
                desktop = null;
            }

            layered = false;
            surface.Dispose();
            base.OnHandleDestroyed(e);
        }

        public void ApplyPreferences()
        {
            prefs.Validate();
            renderer.InvalidateResources();
            hasDrawn = false;
            timer.Interval = Math.Max(1, (int)Math.Round(1000.0 / prefs.Fps));
            TopMost = prefs.OnTop;
            if (desktop != null)
                desktop.Reconcile();
            if (IsHandleCreated)
            {
                ConfigureLayering();
                RenderFrame();
            }
        }

        int RequiredSamples
        {
            get
            {
                int required = prefs.View == "Oscilloscope" ? 1 : prefs.FftSize;
                if (prefs.View != "Bars")
                    required = Math.Max(required, audio.SampleRate * prefs.TimeMs / 1000 + audio.SampleRate / 100 + 2);
                return Math.Min(samples.Length, required);
            }
        }

        float ScaleFactor
        {
            get
            {
                try
                {
                    uint dpi = GetDpiForWindow(Handle);
                    return dpi == 0 ? 1 : dpi / 96f;
                }
                catch
                {
                    return 1;
                }
            }
        }

        void Draw(Graphics g)
        {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Max(.001, Math.Min(.1, now - previousTime));
            previousTime = now;
            bool hasSignal;
            renderer.SampleVersion = audio.Snapshot(samples, RequiredSamples, out hasSignal);
            if (hasSignal)
                lastSignalTime = now;
            int rate = audio.SampleRate;
            renderer.Draw(g, ClientSize, prefs, samples, rate, dt, ScaleFactor);
            lastRenderedRate = rate;
            hasDrawn = true;
        }

        void RenderFrame()
        {
            if (closing || rendering || RecreatingHandle || (desktopStarted && desktop == null) || !IsHandleCreated || !Visible || ClientSize.Width < 2 || ClientSize.Height < 2)
                return;
            bool signal = audio.HasRecentSignal(RequiredSamples);
            double quietFor = clock.Elapsed.TotalSeconds - lastSignalTime;
            if (!NeedsFrame(hasDrawn && restFrameDrawn, signal, lastRenderedRate, audio.SampleRate, quietFor, prefs))
                return;
            double settle = SettlingSeconds(prefs);
            if (!signal && quietFor > settle)
            {
                renderer.ClearAnimation();
                restFrameDrawn = true;
            }
            else
                restFrameDrawn = false;
            if (!layered)
            {
                Invalidate();
                return;
            }

            rendering = true;
            try
            {
                surface.Ensure(ClientSize);
                Draw(surface.Graphics);
                surface.Present(Handle, Location);
                renderError = null;
            }
            catch (Exception ex)
            {
                renderError = "Overlay unavailable: " + ex.Message;
                prefs.BackgroundOpacity = 100;
                prefs.ClickThrough = false;
                if (desktop != null)
                    desktop.Reconcile();
                ConfigureLayering();
                Invalidate();
            }
            finally
            {
                rendering = false;
            }
        }

        internal static bool NeedsFrame(bool drawn, bool signal, int oldRate, int rate, double quietFor, Preferences p)
        {
            double settle = SettlingSeconds(p);
            return !drawn || signal || oldRate != rate || quietFor <= settle;
        }

        static double SettlingSeconds(Preferences preferences)
        {
            return Math.Max(preferences.ReleaseMs * 8, preferences.Peaks ? preferences.PeakHoldMs + preferences.PeakFallMs : 0) / 1000.0 + .25;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!layered && audio != null)
                Draw(e.Graphics);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                closing = true;
                if (settings != null && !settings.IsDisposed)
                    settings.Dispose();
                if (desktop != null)
                {
                    desktop.Dispose();
                    desktop = null;
                }

                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                }

                surface.Dispose();
                renderer.Dispose();
                if (audio != null)
                    audio.Dispose();
            }

            base.Dispose(disposing);
            if (disposing && ownedIcon != null)
            {
                ownedIcon.Dispose();
                ownedIcon = null;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (timer != null)
            {
                renderer.InvalidateResources();
                hasDrawn = false;
                RenderFrame();
            }
        }

        public void SavePreferences()
        {
            Rectangle b = fullscreen ? windowedBounds : (WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
            prefs.Width = b.Width;
            prefs.Height = b.Height;
            prefs.Left = b.Left;
            prefs.Top = b.Top;
            SaveError = prefs.Save();
        }

        public void SetWindowSize(int width, int height)
        {
            if (fullscreen)
                ToggleFullscreen();
            Rectangle work = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(width, work.Width), Math.Min(height, work.Height));
            Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)), Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
        }

        public void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                windowedBounds = Bounds;
                fullscreen = true;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else
            {
                fullscreen = false;
                Bounds = windowedBounds;
            }

            if (settings != null && !settings.IsDisposed)
                settings.BringToFront();
            RenderFrame();
        }

        public void ShowSettings()
        {
            if (closing)
                return;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            if (settings != null && !settings.IsDisposed)
            {
                settings.ActivateSettings();
                return;
            }

            if (desktop != null)
                desktop.SetSettingsOpen(true);
            try
            {
                settings = new SettingsForm(this, prefs);
                settings.FormClosed += delegate
                {
                    settings = null;
                    if (desktop != null)
                        desktop.SetSettingsOpen(false);
                    if (!closing)
                        SavePreferences();
                };
                settings.Show(this);
                settings.Activate();
            }
            catch
            {
                settings = null;
                if (desktop != null)
                    desktop.SetSettingsOpen(false);
                throw;
            }
        }

        public void CustomizeHotkeys()
        {
            ShowSettings();
            if (settings != null)
                settings.ShowHotkeys();
        }

        public bool TryApplyHotkeys(HotkeyBindings candidate, out string error)
        {
            if (!candidate.Validate(out error))
                return false;
            if (desktop == null)
            {
                prefs.Hotkeys = candidate.Clone();
                prefs.Hotkeys.Normalize();
                return true;
            }

            return desktop.TryApplyHotkeys(candidate, out error);
        }

        public bool DispatchShortcut(Keys key)
        {
            switch (prefs.Hotkeys.Resolve(key))
            {
                case AppCommand.Settings:
                    if (settings != null && !settings.IsDisposed)
                        settings.Close();
                    else
                        ShowSettings();
                    return true;
                case AppCommand.Fullscreen:
                    ToggleFullscreen();
                    return true;
                case AppCommand.Quit:
                    Close();
                    return true;
                default:
                    return false;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys key)
        {
            if (key == Keys.Escape && fullscreen)
            {
                ToggleFullscreen();
                return true;
            }

            if (DispatchShortcut(key))
                return true;
            return base.ProcessCmdKey(ref msg, key);
        }

        protected override void WndProc(ref Message msg)
        {
            if (desktop != null && desktop.HandleMessage(msg.Msg, msg.WParam))
            {
                msg.Result = IntPtr.Zero;
                return;
            }

            if (msg.Msg == NonClientHitTest)
            {
                if (fullscreen)
                {
                    msg.Result = new IntPtr((int)HitTest.Client);
                    return;
                }

                long pos = msg.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(pos & 65535), (short)((pos >> 16) & 65535)));
                int edge = Math.Max(5, (int)(5 * ScaleFactor));
                msg.Result = new IntPtr((int)ResizeHitTest(p, ClientSize, edge));
                return;
            }

            if (msg.Msg == NonClientDoubleClick)
            {
                ToggleFullscreen();
                return;
            }

            base.WndProc(ref msg);
        }

        static HitTest ResizeHitTest(Point point, Size size, int edge)
        {
            bool left = point.X < edge, right = point.X >= size.Width - edge;
            if (point.Y < edge)
                return left ? HitTest.TopLeft : right ? HitTest.TopRight : HitTest.Top;
            if (point.Y >= size.Height - edge)
                return left ? HitTest.BottomLeft : right ? HitTest.BottomRight : HitTest.Bottom;
            return left ? HitTest.Left : right ? HitTest.Right : HitTest.Caption;
        }

        [DllImport("user32.dll")]
        static extern uint GetDpiForWindow(IntPtr window);
    }
}
