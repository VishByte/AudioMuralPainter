using System;
using System.Drawing;
using System.Windows.Forms;

namespace Amp
{
    internal sealed class ThresholdPreview : Control
    {
        readonly Preferences prefs;
        int dragging;
        public event EventHandler Changed;
        public ThresholdPreview(Preferences prefs)
        {
            this.prefs = prefs;
            DoubleBuffered = true;
            Cursor = Cursors.SizeWE;
            AccessibleName = "Drag bar color threshold markers";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float width = Math.Max(1, Width - 12), x = 6, y = 8, h = 18;
            using (var low = new SolidBrush(Preferences.ColorOf(prefs.LowColor)))
            using (var mid = new SolidBrush(Preferences.ColorOf(prefs.MidColor)))
            using (var high = new SolidBrush(Preferences.ColorOf(prefs.HighColor)))
            {
                float middle = x + width * (float)prefs.MidThreshold / 100, peak = x + width * (float)prefs.HighThreshold / 100;
                g.FillRectangle(low, x, y, middle - x, h);
                g.FillRectangle(mid, middle, y, peak - middle, h);
                g.FillRectangle(high, peak, y, x + width - peak, h);
                using (var pen = new Pen(Color.White, 2))
                {
                    g.DrawLine(pen, middle, y - 4, middle, y + h + 5);
                    g.DrawLine(pen, peak, y - 4, peak, y + h + 5);
                }

                using (var brush = new SolidBrush(Color.FromArgb(147, 164, 155)))
                {
                    string unit = prefs.ThresholdUnit == "Percent" ? "%" : " dBFS";
                    double a = prefs.ThresholdUnit == "Percent" ? prefs.MidThreshold : prefs.ThresholdDb(prefs.MidThreshold), b = prefs.ThresholdUnit == "Percent" ? prefs.HighThreshold : prefs.ThresholdDb(prefs.HighThreshold);
                    g.DrawString("Middle " + a.ToString("0.#") + unit + "     Peak " + b.ToString("0.#") + unit, Font, brush, 6, 32);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            double pct = 100.0 * (e.X - 6) / Math.Max(1, Width - 12);
            dragging = Math.Abs(pct - prefs.MidThreshold) < Math.Abs(pct - prefs.HighThreshold) ? 1 : 2;
            Capture = true;
            UpdateValue(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging != 0)
                UpdateValue(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = 0;
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
                dragging = 0;
        }

        void UpdateValue(int x)
        {
            double pct = Math.Round(1000.0 * (x - 6) / Math.Max(1, Width - 12)) / 10;
            if (dragging == 1)
                prefs.MidThreshold = Math.Max(1, Math.Min(prefs.HighThreshold - 1, pct));
            else
                prefs.HighThreshold = Math.Max(prefs.MidThreshold + 1, Math.Min(99, pct));
            Invalidate();
            if (Changed != null)
                Changed(this, EventArgs.Empty);
        }
    }
}
