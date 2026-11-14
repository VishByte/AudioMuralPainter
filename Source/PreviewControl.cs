using System;
using System.Drawing;
using System.Windows.Forms;

namespace Amp
{
    internal sealed class PreviewControl : Control
    {
        readonly Preferences prefs;
        readonly SceneRenderer renderer = new SceneRenderer();
        readonly Timer timer = new Timer
        {
            Interval = 50
        };
        readonly float[] samples = new float[65536];
        double time;
        Bitmap frame;
        Graphics frameGraphics;
        readonly SolidBrush checkerA = new SolidBrush(Color.FromArgb(37, 45, 40)), checkerB = new SolidBrush(Color.FromArgb(50, 59, 53));
        public PreviewControl(Preferences prefs)
        {
            this.prefs = prefs;
            DoubleBuffered = true;
            timer.Tick += delegate
            {
                time += .05;
                Invalidate();
            };
            timer.Start();
            Disposed += delegate
            {
                timer.Dispose();
                renderer.Dispose();
                if (frameGraphics != null)
                    frameGraphics.Dispose();
                if (frame != null)
                    frame.Dispose();
                checkerA.Dispose();
                checkerB.Dispose();
            };
            AccessibleName = "Visualizer preview with example signal";
        }

        public void RefreshSettings()
        {
            renderer.InvalidateResources();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 2 || Height < 2)
                return;
            int used = Math.Min(samples.Length, Math.Max(prefs.FftSize, 48000 * prefs.TimeMs / 1000 + 600));
            double level = .1 + .065 * Math.Sin(time * 2);
            for (int i = samples.Length - used; i < samples.Length; i++)
            {
                double phase = (i + time * 48000) / 48000;
                samples[i] = (float)(level * Math.Sin(phase * 2 * Math.PI * 110) + .065 * Math.Sin(phase * 2 * Math.PI * 440) + .025 * Math.Sin(phase * 2 * Math.PI * 1800) + .01 * Math.Sin(phase * 2 * Math.PI * 6500));
            }

            // Composite a real transparent render onto a checkerboard for a useful opacity preview.
            if (frame == null || frame.Size != Size)
            {
                if (frameGraphics != null)
                    frameGraphics.Dispose();
                if (frame != null)
                    frame.Dispose();
                frame = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                frameGraphics = Graphics.FromImage(frame);
            }

            renderer.Draw(frameGraphics, Size, prefs, samples, 48000, .05f, 1);
            for (int y = 0; y < Height; y += 12)
                for (int x = 0; x < Width; x += 12)
                    e.Graphics.FillRectangle(((x / 12 + y / 12) % 2 == 0) ? checkerA : checkerB, x, y, 12, 12);
            e.Graphics.DrawImageUnscaled(frame, 0, 0);
        }
    }
}
