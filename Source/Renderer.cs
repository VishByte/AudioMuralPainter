using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace Amp
{
    internal struct SceneLayout
    {
        public RectangleF Content, Scope, Bars;
        public static SceneLayout Make(Size size, Preferences p, float scale)
        {
            float pad = Math.Min(p.Padding * scale, Math.Min(size.Width, size.Height) / 4f);
            var layout = new SceneLayout
            {
                Content = new RectangleF(pad, pad, Math.Max(1, size.Width - pad * 2), Math.Max(1, size.Height - pad * 2))
            };
            if (p.View == "Both")
            {
                float gap = Math.Min(p.ViewGap * scale, layout.Content.Height / 4);
                float available = layout.Content.Height - gap, scope = available * p.ScopeShare / 100;
                layout.Scope = new RectangleF(pad, pad, layout.Content.Width, scope);
                layout.Bars = new RectangleF(pad, pad + scope + gap, layout.Content.Width, available - scope);
            }
            else if (p.View == "Oscilloscope")
                layout.Scope = layout.Content;
            else
                layout.Bars = layout.Content;
            return layout;
        }
    }

    internal static class MeterMath
    {
        public static double Clamp(double value)
        {
            return Math.Max(0, Math.Min(1, value));
        }

        public static void FrequencyBounds(Preferences p, int rate, out double low, out double high)
        {
            high = Math.Min(p.MaxFrequency, Math.Max(2, rate * .49));
            low = Math.Min(p.MinFrequency, high - 1);
        }

        public static double FrequencyAt(Preferences p, double t, int rate)
        {
            double low, high;
            FrequencyBounds(p, rate, out low, out high);
            return p.FrequencyScale == "Linear" ? low + (high - low) * t : low * Math.Pow(high / low, t);
        }

        public static double Level(double amplitude, Preferences p)
        {
            double rawDb = 20 * Math.Log10(Math.Max(1e-12, amplitude));
            if (p.GateEnabled && rawDb < p.GateDb)
            {
                if (p.GateSoftnessDb == 0 || rawDb <= p.GateDb - p.GateSoftnessDb)
                    return 0;
                amplitude *= Clamp((rawDb - (p.GateDb - p.GateSoftnessDb)) / p.GateSoftnessDb);
            }

            double db = 20 * Math.Log10(Math.Max(1e-12, amplitude)) + p.EffectiveBarGain;
            return Clamp((db - p.MeterFloorDb) / -p.MeterFloorDb);
        }

        public static double Smooth(double current, double target, Preferences p, double dt)
        {
            double tau = (target > current ? p.AttackMs : p.ReleaseMs) / 1000.0;
            return current + (target - current) * (1 - Math.Exp(-dt / tau));
        }

        public static Color LevelColor(Preferences p, double fraction)
        {
            return Preferences.ColorOf(fraction >= p.HighThreshold / 100 ? p.HighColor : fraction >= p.MidThreshold / 100 ? p.MidColor : p.LowColor);
        }

        public static string FrequencyLabel(double hz)
        {
            return hz >= 1000 ? (hz / 1000).ToString("0.##", CultureInfo.InvariantCulture) + " kHz" : hz.ToString("0.#", CultureInfo.InvariantCulture) + " Hz";
        }
    }
}
