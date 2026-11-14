using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Collections.Generic;

namespace Amp
{
    internal sealed class SceneRenderer : IDisposable
    {
        readonly Spectrum spectrum = new Spectrum();
        double[] levels = new double[0], peaks = new double[0], holds = new double[0], frequencies = new double[0];
        PointF[] path = new PointF[0];
        RectangleF[] lowRects, midRects, highRects, allRects, localRects, idleRects;
        int previousLow, previousMid, previousHigh, previousAll, previousLocal;
        string bandSignature = "";
        Bitmap backdrop;
        Bitmap[] axisImages = new Bitmap[0];
        Point[] axisLocations = new Point[0];
        Pen gridPen;
        Color background;
        bool scopeGrid, barsGrid;
        int scopeTicks, barTicks;
        Font font;
        Brush low, mid, high, idle, globalGradient;
        Brush[] perBarGradients;
        Pen scopeLine, scopeGlowA, scopeGlowB;
        RectangleF scopeArea, barArea;
        Size resourceSize;
        float resourceScale;
        int resourceRate;
        bool resourcesDirty = true;
        int count, segments;
        float stride, gap, barWidth, step;
        public long SampleVersion = long.MinValue;
        long fftVersion = long.MinValue;
        int fftSize;
        public void InvalidateResources()
        {
            resourcesDirty = true;
            fftVersion = long.MinValue;
        }

        public void ClearAnimation()
        {
            Array.Clear(levels, 0, levels.Length);
            Array.Clear(peaks, 0, peaks.Length);
            Array.Clear(holds, 0, holds.Length);
            fftVersion = long.MinValue;
        }

        static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);
        }

        internal static Brush Gradient(RectangleF rect, Preferences p, bool scope, int alpha)
        {
            bool shared = p.GradientCoverage == "Both views";
            Color first = Preferences.ColorOf(scope && !shared ? p.ScopeColor : p.LowColor), middle = Preferences.ColorOf(scope && !shared ? p.ScopeGradientMid : p.MidColor), end = Preferences.ColorOf(scope && !shared ? p.ScopeGradientEnd : p.HighColor);
            PointF a, b;
            if (p.GradientDirection == "Horizontal")
            {
                a = new PointF(rect.Left, rect.Top);
                b = new PointF(rect.Right, rect.Top);
            }
            else
            {
                a = new PointF(rect.Left, rect.Bottom);
                b = new PointF(rect.Left, rect.Top);
            }

            if (p.GradientReverse)
            {
                PointF swap = a;
                a = b;
                b = swap;
            }

            if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < 1)
                b.X = a.X + 1;
            var brush = new LinearGradientBrush(a, b, Color.Black, Color.White)
            {
                WrapMode = WrapMode.TileFlipXY
            };
            if (scope && !shared)
                brush.InterpolationColors = new ColorBlend
                {
                    Positions = new[]
                    {
                        0f,
                        .5f,
                        1f
                    },
                    Colors = new[]
                    {
                        Alpha(first, alpha),
                        Alpha(middle, alpha),
                        Alpha(end, alpha)
                    }
                };
            else
                brush.InterpolationColors = new ColorBlend
                {
                    Positions = new[]
                    {
                        0f,
                        (float)p.MidThreshold / 100,
                        (float)p.HighThreshold / 100,
                        1f
                    },
                    Colors = new[]
                    {
                        Alpha(first, alpha),
                        Alpha(middle, alpha),
                        Alpha(end, alpha),
                        Alpha(end, alpha)
                    }
                };
            return brush;
        }

        static Brush ScopeBrush(RectangleF area, Preferences p, int alpha)
        {
            return p.ScopeStyle == "Gradient" ? Gradient(area, p, true, alpha) : new SolidBrush(Alpha(Preferences.ColorOf(p.ScopeColor), alpha));
        }

        static int TickCount(RectangleF area, Preferences p, Font font)
        {
            return Math.Max(2, Math.Min(p.LabelDensity, (int)(area.Width / Math.Max(70, font.Size * 8))));
        }

        static void AxisText(Graphics g, string text, Font font, Brush brush, float x, float y, int alignment, RectangleF limits)
        {
            var measured = g.MeasureString(text, font);
            if (alignment == 0)
                x -= measured.Width / 2;
            else if (alignment == 1)
                x -= measured.Width;
            x = Math.Max(limits.Left, Math.Min(x, limits.Right - measured.Width));
            g.DrawString(text, font, brush, x, y);
        }

        void EnsureResources(Size size, Preferences p, int rate, float scale)
        {
            if (!resourcesDirty && resourceSize == size && resourceScale == scale && resourceRate == rate)
                return;
            DisposeResources();
            resourceSize = size;
            resourceScale = scale;
            resourceRate = rate;
            SceneLayout layout = SceneLayout.Make(size, p, scale);
            font = new Font("Segoe UI", p.LabelSize * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            background = Color.FromArgb(Math.Max(1, (int)Math.Round(p.BackgroundOpacity * 2.55)), 0, 0, 0);
            scopeGrid = p.ScopeGrid;
            barsGrid = p.BarsGrid;
            gridPen = new Pen(Alpha(Preferences.ColorOf(p.LabelColor), p.GridOpacity * 255 / 100), scale);
            backdrop = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(backdrop))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.FromArgb(Math.Max(1, (int)Math.Round(p.BackgroundOpacity * 2.55)), 0, 0, 0));
                g.CompositingMode = CompositingMode.SourceOver;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var label = new SolidBrush(Alpha(Preferences.ColorOf(p.LabelColor), p.LabelOpacity * 255 / 100)))
                using (var grid = new Pen(Alpha(Preferences.ColorOf(p.LabelColor), p.GridOpacity * 255 / 100), scale))
                {
                    scopeArea = RectangleF.Empty;
                    barArea = RectangleF.Empty;
                    if (layout.Scope.Width > 0)
                    {
                        RectangleF bounds = layout.Scope;
                        float footer = p.ScopeTimeLabels ? font.GetHeight(g) + 5 * scale : 0, axis = p.ScopeAmplitudeLabels ? 38 * scale : 0;
                        scopeArea = new RectangleF(bounds.Left, bounds.Top, Math.Max(1, bounds.Width - axis), Math.Max(1, bounds.Height - footer));
                        int ticks = TickCount(scopeArea, p, font);
                        scopeTicks = ticks;
                        if (p.ScopeGrid)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float y = scopeArea.Top + scopeArea.Height * i / 4;
                                g.DrawLine(grid, scopeArea.Left, y, scopeArea.Right, y);
                            }

                            for (int i = 0; i < ticks; i++)
                            {
                                float x = scopeArea.Left + scopeArea.Width * i / (ticks - 1);
                                g.DrawLine(grid, x, scopeArea.Top, x, scopeArea.Bottom);
                            }
                        }

                        if (p.ScopeTimeLabels)
                            for (int i = 0; i < ticks; i++)
                            {
                                double ms = -p.TimeMs * (1 - (double)i / (ticks - 1));
                                AxisText(g, ms.ToString("0.#", CultureInfo.InvariantCulture) + " ms", font, label, scopeArea.Left + scopeArea.Width * i / (ticks - 1), scopeArea.Bottom + 3 * scale, i == 0 ? -1 : i == ticks - 1 ? 1 : 0, bounds);
                            }

                        if (p.ScopeAmplitudeLabels)
                            for (int i = 0; i < 5; i++)
                            {
                                double amplitude = 1 - i * .5;
                                string text = amplitude.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture);
                                float y = Math.Max(bounds.Top, Math.Min(scopeArea.Top + scopeArea.Height * i / 4 - font.GetHeight(g) / 2, scopeArea.Bottom - font.GetHeight(g)));
                                g.DrawString(text, font, label, scopeArea.Right + 5 * scale, y);
                            }

                        RectangleF gradient = p.GradientCoverage == "Both views" ? layout.Content : scopeArea;
                        using (var brush = ScopeBrush(gradient, p, 255))
                            scopeLine = new Pen(brush, (float)p.WaveThickness * scale)
                            {
                                LineJoin = LineJoin.Round
                            };
                        if (p.WaveGlow > 0)
                        {
                            using (var brush = ScopeBrush(gradient, p, p.WaveGlow * 64 / 100))
                                scopeGlowA = new Pen(brush, (float)(p.WaveThickness + 6) * scale);
                            using (var brush = ScopeBrush(gradient, p, p.WaveGlow * 145 / 100))
                                scopeGlowB = new Pen(brush, (float)(p.WaveThickness + 2) * scale);
                        }
                    }

                    if (layout.Bars.Width > 0)
                    {
                        RectangleF bounds = layout.Bars;
                        float footer = p.BarsLabels ? font.GetHeight(g) + 5 * scale : 0;
                        barArea = new RectangleF(bounds.Left, bounds.Top, bounds.Width, Math.Max(1, bounds.Height - footer));
                        int ticks = TickCount(barArea, p, font);
                        barTicks = ticks;
                        if (p.BarsGrid)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float y = barArea.Top + barArea.Height * i / 4;
                                g.DrawLine(grid, barArea.Left, y, barArea.Right, y);
                            }

                            for (int i = 0; i < ticks; i++)
                            {
                                float x = barArea.Left + barArea.Width * i / (ticks - 1);
                                g.DrawLine(grid, x, barArea.Top, x, barArea.Bottom);
                            }
                        }

                        if (p.BarsLabels)
                            for (int i = 0; i < ticks; i++)
                                AxisText(g, MeterMath.FrequencyLabel(MeterMath.FrequencyAt(p, (double)i / (ticks - 1), rate)), font, label, barArea.Left + barArea.Width * i / (ticks - 1), barArea.Bottom + 3 * scale, i == 0 ? -1 : i == ticks - 1 ? 1 : 0, bounds);
                    }
                }
            }

            // Cache only the narrow axis strips. Copying a whole transparent window bitmap
            // each frame costs more than clearing it and drawing a few cached grid lines.
            var images = new List<Bitmap>();
            var locations = new List<Point>();
            if (layout.Scope.Width > 0 && p.ScopeTimeLabels)
                AddAxis(images, locations, RectangleF.FromLTRB(layout.Scope.Left, scopeArea.Bottom - 1, layout.Scope.Right, layout.Scope.Bottom));
            if (layout.Scope.Width > 0 && p.ScopeAmplitudeLabels)
                AddAxis(images, locations, RectangleF.FromLTRB(scopeArea.Right - 1, layout.Scope.Top, layout.Scope.Right, scopeArea.Bottom - 2));
            if (layout.Bars.Width > 0 && p.BarsLabels)
                AddAxis(images, locations, RectangleF.FromLTRB(layout.Bars.Left, barArea.Bottom - 1, layout.Bars.Right, layout.Bars.Bottom));
            axisImages = images.ToArray();
            axisLocations = locations.ToArray();
            backdrop.Dispose();
            backdrop = null;
            if (barArea.Width > 0)
            {
                count = Math.Min(p.Bars, Math.Max(4, (int)(barArea.Width / (2 * scale))));
                string signature = count + "/" + p.MinFrequency + "/" + p.MaxFrequency + "/" + p.FrequencyScale + "/" + p.FftSize + "/" + rate;
                if (bandSignature != signature)
                {
                    levels = new double[count];
                    peaks = new double[count];
                    holds = new double[count];
                    bandSignature = signature;
                }

                frequencies = new double[count + 1];
                for (int i = 0; i <= count; i++)
                    frequencies[i] = MeterMath.FrequencyAt(p, (double)i / count, rate);
                stride = barArea.Width / count;
                gap = Math.Min((float)p.BarGap * scale, Math.Max(0, stride - scale));
                barWidth = Math.Max(scale, stride - gap);
                step = (float)(p.SegmentHeight + p.SegmentGap) * scale;
                segments = Math.Max(1, (int)Math.Ceiling(barArea.Height / step));
                int capacity = count * (p.Segmented ? segments + 1 : 4);
                lowRects = new RectangleF[capacity];
                midRects = new RectangleF[capacity];
                highRects = new RectangleF[capacity];
                allRects = new RectangleF[capacity];
                localRects = new RectangleF[Math.Max(4, segments + 1)];
                idleRects = new RectangleF[count];
                previousLow = previousMid = previousHigh = previousAll = previousLocal = 0;
                low = new SolidBrush(Preferences.ColorOf(p.LowColor));
                mid = new SolidBrush(Preferences.ColorOf(p.MidColor));
                high = new SolidBrush(Preferences.ColorOf(p.HighColor));
                idle = new SolidBrush(Alpha(Preferences.ColorOf(p.LowColor), p.IdleOpacity * 255 / 100));
                float idleHeight = Math.Min(barArea.Height, Math.Max(scale, (float)p.SegmentHeight * scale));
                for (int i = 0; i < count; i++)
                    idleRects[i] = new RectangleF(barArea.Left + i * stride + gap * .5f, barArea.Bottom - idleHeight, barWidth, idleHeight);
                if (p.BarStyle == "Gradient")
                {
                    if (p.GradientCoverage == "Each bar")
                    {
                        perBarGradients = new Brush[count];
                        for (int i = 0; i < count; i++)
                            perBarGradients[i] = Gradient(new RectangleF(barArea.Left + i * stride + gap * .5f, barArea.Top, barWidth, barArea.Height), p, false, 255);
                    }
                    else
                        globalGradient = Gradient(p.GradientCoverage == "Both views" ? layout.Content : barArea, p, false, 255);
                }
            }

            resourcesDirty = false;
        }

        public void Draw(Graphics g, Size size, Preferences p, float[] samples, int rate, double dt, float scale)
        {
            EnsureResources(size, p, rate, scale);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(background);
            g.CompositingMode = CompositingMode.SourceOver;
            if (p.View == "Oscilloscope")
                DrawScopeAxes(g, p, scale);
            else
            {
                if (scopeGrid && scopeArea.Width > 0)
                    DrawGrid(g, scopeArea, scopeTicks);
                if (barsGrid && barArea.Width > 0)
                    DrawGrid(g, barArea, barTicks);
                g.CompositingMode = CompositingMode.SourceCopy;
                for (int i = 0; i < axisImages.Length; i++)
                    g.DrawImageUnscaled(axisImages[i], axisLocations[i]);
                g.CompositingMode = CompositingMode.SourceOver;
            }

            if (scopeArea.Width > 0)
                DrawScope(g, p, samples, rate);
            if (barArea.Width > 0)
                DrawBars(g, p, samples, rate, dt, scale);
        }

        void DrawScopeAxes(Graphics g, Preferences p, float scale)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            if (scopeGrid)
                DrawGrid(g, scopeArea, scopeTicks);
            var bounds = SceneLayout.Make(resourceSize, p, scale).Scope;
            using (var label = new SolidBrush(Alpha(Preferences.ColorOf(p.LabelColor), p.LabelOpacity * 255 / 100)))
            {
                if (p.ScopeTimeLabels)
                    for (int i = 0; i < scopeTicks; i++)
                    {
                        double ms = -p.TimeMs * (1 - (double)i / (scopeTicks - 1));
                        AxisText(g, ms.ToString("0.#", CultureInfo.InvariantCulture) + " ms", font, label, scopeArea.Left + scopeArea.Width * i / (scopeTicks - 1), scopeArea.Bottom + 3 * scale, i == 0 ? -1 : i == scopeTicks - 1 ? 1 : 0, bounds);
                    }

                if (p.ScopeAmplitudeLabels)
                    for (int i = 0; i < 5; i++)
                    {
                        double amplitude = 1 - i * .5;
                        string text = amplitude.ToString("+0.#;-0.#;0", CultureInfo.InvariantCulture);
                        float y = Math.Max(bounds.Top, Math.Min(scopeArea.Top + scopeArea.Height * i / 4 - font.GetHeight(g) / 2, scopeArea.Bottom - font.GetHeight(g)));
                        g.DrawString(text, font, label, scopeArea.Right + 5 * scale, y);
                    }
            }
        }

        void AddAxis(List<Bitmap> images, List<Point> locations, RectangleF bounds)
        {
            var rect = Rectangle.Intersect(new Rectangle(0, 0, resourceSize.Width, resourceSize.Height), Rectangle.FromLTRB((int)Math.Floor(bounds.Left), (int)Math.Floor(bounds.Top), (int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom)));
            if (rect.Width < 1 || rect.Height < 1)
                return;
            images.Add(backdrop.Clone(rect, PixelFormat.Format32bppPArgb));
            locations.Add(rect.Location);
        }

        void DrawGrid(Graphics g, RectangleF area, int ticks)
        {
            for (int i = 0; i < 5; i++)
            {
                float y = area.Top + area.Height * i / 4;
                g.DrawLine(gridPen, area.Left, y, area.Right, y);
            }

            for (int i = 0; i < ticks; i++)
            {
                float x = area.Left + area.Width * i / (ticks - 1);
                g.DrawLine(gridPen, x, area.Top, x, area.Bottom);
            }
        }

        void DrawScope(Graphics g, Preferences p, float[] samples, int rate)
        {
            int span = Math.Min(samples.Length - rate / 100 - 2, Math.Max(16, rate * p.TimeMs / 1000));
            int start = samples.Length - span;
            for (int i = start; i > Math.Max(1, samples.Length - span - rate / 100); i--)
                if (samples[i - 1] <= 0 && samples[i] > 0)
                {
                    start = i;
                    break;
                }

            int n = Math.Min(span, Math.Min(p.WavePoints, Math.Max(128, (int)scopeArea.Width * 2)));
            if (path.Length != n)
                path = new PointF[n];
            double gain = Math.Pow(10, p.EffectiveScopeGain / 20.0);
            for (int i = 0; i < n; i++)
            {
                double index = start + (double)i * (span - 1) / (n - 1);
                int a = (int)index, b = Math.Min(samples.Length - 1, a + 1);
                double v = Math.Max(-1, Math.Min(1, (samples[a] * (1 - (index - a)) + samples[b] * (index - a)) * gain));
                path[i] = new PointF(scopeArea.Left + i * scopeArea.Width / (n - 1), scopeArea.Top + scopeArea.Height * .5f - (float)v * scopeArea.Height * .46f);
            }

            var state = g.Save();
            g.SetClip(scopeArea);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (p.View == "Oscilloscope")
            {
                RectangleF gradient = p.GradientCoverage == "Both views" ? SceneLayout.Make(resourceSize, p, resourceScale).Content : scopeArea;
                if (p.WaveGlow > 0)
                {
                    using (var brush = ScopeBrush(gradient, p, p.WaveGlow * 64 / 100))
                    using (var pen = new Pen(brush, (float)(p.WaveThickness + 6) * resourceScale))
                        NativeDrawing.Lines(g, pen, path);
                    using (var brush = ScopeBrush(gradient, p, p.WaveGlow * 145 / 100))
                    using (var pen = new Pen(brush, (float)(p.WaveThickness + 2) * resourceScale))
                        NativeDrawing.Lines(g, pen, path);
                }

                using (var brush = ScopeBrush(gradient, p, 255))
                using (var pen = new Pen(brush, (float)p.WaveThickness * resourceScale))
                {
                    pen.LineJoin = LineJoin.Round;
                    NativeDrawing.Lines(g, pen, path);
                }
            }
            else
            {
                if (scopeGlowA != null)
                {
                    NativeDrawing.Lines(g, scopeGlowA, path);
                    NativeDrawing.Lines(g, scopeGlowB, path);
                }

                NativeDrawing.Lines(g, scopeLine, path);
            }

            g.Restore(state);
        }

        static void FillBatch(Graphics g, Brush brush, RectangleF[] rectangles, int used, ref int previous)
        {
            if (used < previous)
                Array.Clear(rectangles, used, previous - used);
            if (used > 0)
                NativeDrawing.Rectangles(g, brush, rectangles, used);
            previous = used;
        }

        void DrawBars(Graphics g, Preferences p, float[] samples, int rate, double dt, float scale)
        {
            if (SampleVersion == long.MinValue || fftVersion != SampleVersion || fftSize != p.FftSize)
            {
                spectrum.Compute(samples, p.FftSize);
                fftVersion = SampleVersion;
                fftSize = p.FftSize;
            }

            var state = g.Save();
            g.SetClip(barArea);
            if (p.IdleBars && p.IdleOpacity > 0)
                NativeDrawing.Rectangles(g, idle, idleRects, idleRects.Length);
            int nl = 0, nm = 0, nh = 0, ng = 0;
            for (int i = 0; i < count; i++)
            {
                double target = MeterMath.Level(spectrum.Band(frequencies[i], frequencies[i + 1], rate, p.FftSize), p);
                levels[i] = MeterMath.Smooth(levels[i], target, p, dt);
                if (levels[i] > peaks[i])
                {
                    peaks[i] = levels[i];
                    holds[i] = p.PeakHoldMs / 1000.0;
                }
                else if (holds[i] > 0)
                    holds[i] -= dt;
                else
                    peaks[i] = Math.Max(levels[i], peaks[i] - dt * 1000 / p.PeakFallMs);
                float x = barArea.Left + i * stride + gap * .5f;
                int localCount = 0;
                if (p.Segmented)
                {
                    int lit = (int)Math.Floor(levels[i] * segments + .001);
                    for (int s = 0; s < lit; s++)
                    {
                        double fraction = (s + .5) / segments;
                        float y = Math.Max(barArea.Top, barArea.Bottom - (s + 1) * step + (float)p.SegmentGap * scale);
                        var rect = new RectangleF(x, y, barWidth, Math.Min((float)p.SegmentHeight * scale, barArea.Bottom - y));
                        if (perBarGradients != null)
                            localRects[localCount++] = rect;
                        else if (globalGradient != null)
                            allRects[ng++] = rect;
                        else if (fraction >= p.HighThreshold / 100)
                            highRects[nh++] = rect;
                        else if (fraction >= p.MidThreshold / 100)
                            midRects[nm++] = rect;
                        else
                            lowRects[nl++] = rect;
                    }
                }
                else if (levels[i] * barArea.Height > .01)
                {
                    var rect = new RectangleF(x, barArea.Bottom - (float)levels[i] * barArea.Height, barWidth, (float)levels[i] * barArea.Height);
                    if (perBarGradients != null)
                        localRects[localCount++] = rect;
                    else if (globalGradient != null)
                        allRects[ng++] = rect;
                    else
                    {
                        AddZone(lowRects, ref nl, x, levels[i], 0, p.MidThreshold / 100);
                        AddZone(midRects, ref nm, x, levels[i], p.MidThreshold / 100, p.HighThreshold / 100);
                        AddZone(highRects, ref nh, x, levels[i], p.HighThreshold / 100, 1);
                    }
                }

                if (p.Peaks && peaks[i] > .01)
                {
                    var rect = new RectangleF(x, Math.Max(barArea.Top, barArea.Bottom - (float)peaks[i] * barArea.Height), barWidth, Math.Max(1, 1.5f * scale));
                    if (perBarGradients != null)
                        localRects[localCount++] = rect;
                    else if (globalGradient != null)
                        allRects[ng++] = rect;
                    else if (peaks[i] >= p.HighThreshold / 100)
                        highRects[nh++] = rect;
                    else if (peaks[i] >= p.MidThreshold / 100)
                        midRects[nm++] = rect;
                    else
                        lowRects[nl++] = rect;
                }

                if (perBarGradients != null)
                    FillBatch(g, perBarGradients[i], localRects, localCount, ref previousLocal);
            }

            if (globalGradient != null)
                FillBatch(g, globalGradient, allRects, ng, ref previousAll);
            else if (perBarGradients == null)
            {
                FillBatch(g, low, lowRects, nl, ref previousLow);
                FillBatch(g, mid, midRects, nm, ref previousMid);
                FillBatch(g, high, highRects, nh, ref previousHigh);
            }

            g.Restore(state);
        }

        void AddZone(RectangleF[] rectangles, ref int used, float x, double level, double bottom, double top)
        {
            double end = Math.Min(level, top);
            if (end > bottom)
                rectangles[used++] = new RectangleF(x, barArea.Bottom - (float)end * barArea.Height, barWidth, (float)(end - bottom) * barArea.Height);
        }

        void DisposeResources()
        {
            foreach (var axis in axisImages)
                axis.Dispose();
            axisImages = new Bitmap[0];
            axisLocations = new Point[0];
            if (gridPen != null)
            {
                gridPen.Dispose();
                gridPen = null;
            }

            if (backdrop != null)
            {
                backdrop.Dispose();
                backdrop = null;
            }

            if (font != null)
            {
                font.Dispose();
                font = null;
            }

            if (scopeLine != null)
            {
                scopeLine.Dispose();
                scopeLine = null;
            }

            if (scopeGlowA != null)
            {
                scopeGlowA.Dispose();
                scopeGlowA = null;
            }

            if (scopeGlowB != null)
            {
                scopeGlowB.Dispose();
                scopeGlowB = null;
            }

            if (low != null)
            {
                low.Dispose();
                low = null;
            }

            if (mid != null)
            {
                mid.Dispose();
                mid = null;
            }

            if (high != null)
            {
                high.Dispose();
                high = null;
            }

            if (idle != null)
            {
                idle.Dispose();
                idle = null;
            }

            if (globalGradient != null)
            {
                globalGradient.Dispose();
                globalGradient = null;
            }

            if (perBarGradients != null)
            {
                foreach (var brush in perBarGradients)
                    brush.Dispose();
                perBarGradients = null;
            }
        }

        public void Dispose()
        {
            DisposeResources();
            resourcesDirty = true;
        }
    }
}
