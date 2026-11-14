using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Amp
{
    internal static class Tests
    {
        static readonly List<string> checks = new List<string>();
        static void Assert(bool ok, string message)
        {
            if (!ok)
                throw new Exception(message);
            checks.Add("PASS  " + message);
        }

        static float[] Signal()
        {
            var samples = new float[65536];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)(.32 * Math.Sin(2 * Math.PI * 1500 * i / 48000) + .15 * Math.Sin(2 * Math.PI * 3000 * i / 48000));
            return samples;
        }

        static Bitmap Render(Preferences p, float[] samples, int width, int height)
        {
            p.Validate();
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var renderer = new SceneRenderer())
            using (var g = Graphics.FromImage(bitmap))
                for (int frame = 0; frame < 3; frame++)
                    renderer.Draw(g, new Size(width, height), p, samples, 48000, .1, 1);
            return bitmap;
        }

        static int Count(Bitmap bitmap, Color color)
        {
            int count = 0;
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                for (int i = 0; i < bytes.Length; i += 4)
                    if (bytes[i] == color.B && bytes[i + 1] == color.G && bytes[i + 2] == color.R && bytes[i + 3] == 255)
                        count++;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return count;
        }

        static ulong Hash(Bitmap bitmap)
        {
            ulong hash = 1469598103934665603;
            for (int y = 0; y < bitmap.Height; y += 3)
                for (int x = 0; x < bitmap.Width; x += 3)
                    hash = unchecked((hash ^ (uint)bitmap.GetPixel(x, y).ToArgb()) * 1099511628211);
            return hash;
        }

        static Control Find(Control root, string text)
        {
            foreach (Control child in root.Controls)
            {
                if (child is Label && child.Text == text)
                    return child;
                Control found = Find(child, text);
                if (found != null)
                    return found;
            }

            return null;
        }

        sealed class FakeRegistrar : IHotkeyRegistrar
        {
            public readonly Dictionary<int, Keys> Active = new Dictionary<int, Keys>();
            public readonly List<string> Events = new List<string>();
            public bool FailNext;
            public bool Register(IntPtr window, int id, Keys key, out string error)
            {
                Events.Add("register:" + id);
                if (FailNext)
                {
                    FailNext = false;
                    error = "Shortcut already in use";
                    return false;
                }

                Active.Add(id, key);
                error = null;
                return true;
            }

            public void Unregister(IntPtr window, int id)
            {
                Events.Add("unregister:" + id);
                Active.Remove(id);
            }
        }

        static void InteractionChecks(string folder)
        {
            Keys key;
            string error;
            Assert(Shortcut.TryParse("Ctrl+Alt+F1", out key) && key == (Keys.Control | Keys.Alt | Keys.F1), "Recovery shortcut parses correctly");
            Assert(Shortcut.Modifiers(key) == 3 && Shortcut.Matches(Shortcut.Format(key), key), "Shortcut formatting and Windows modifier mapping");
            var bindings = new HotkeyBindings();
            Assert(bindings.Validate(out error), "Default hotkey bindings are valid");
            Assert(bindings.Resolve(Keys.F1) == AppCommand.Settings && bindings.Resolve(Keys.F11) == AppCommand.Fullscreen && bindings.Resolve(Keys.Control | Keys.Q) == AppCommand.Quit, "Focused commands resolve from bindings");
            bindings.Settings = "Ctrl+Shift+S";
            bindings.FullscreenAlternate = "";
            Assert(bindings.Validate(out error) && bindings.Resolve(Keys.F1) == AppCommand.None && bindings.Resolve(Keys.Control | Keys.Shift | Keys.S) == AppCommand.Settings, "Custom focused shortcuts replace defaults and optional shortcuts can be cleared");
            bindings.Quit = bindings.Settings;
            Assert(!bindings.Validate(out error), "Duplicate shortcuts are rejected");
            bindings = new HotkeyBindings
            {
                GlobalSettings = "F2"
            };
            Assert(!bindings.Validate(out error), "Global recovery shortcut requires Ctrl or Alt");
            bindings = new HotkeyBindings
            {
                GlobalSettings = ""
            };
            Assert(!bindings.Validate(out error), "Global recovery shortcut cannot be removed");
            bindings = new HotkeyBindings
            {
                Quit = "Alt+F4"
            };
            Assert(!bindings.Validate(out error), "Fixed Windows/navigation shortcuts stay reserved");
            bindings = new HotkeyBindings
            {
                Settings = "broken shortcut"
            };
            bindings.Normalize();
            Assert(bindings.Settings == "F1" && bindings.GlobalSettings == "Ctrl+Alt+F1", "Invalid saved shortcuts recover to defaults");
            Assert(InteractionPolicy.ClickThrough(true, true, false), "Requested click-through activates with a recovery shortcut");
            Assert(!InteractionPolicy.ClickThrough(true, true, true), "Opening settings suspends click-through");
            Assert(!InteractionPolicy.ClickThrough(true, false, false), "Unavailable recovery shortcut keeps the overlay clickable");
            Assert(!InteractionPolicy.ClickThrough(false, true, false), "Tray/checkbox opt-out keeps the overlay clickable");
            Assert(InteractionPolicy.Layered(100, true) && !InteractionPolicy.Layered(100, false) && InteractionPolicy.Layered(25, false), "Click-through supports opaque and translucent backgrounds");
            long style = LayeredSurface.WindowStyle(8, false, true);
            Assert((style & 0x80020) == 0x80020 && (style & 8) == 8, "Click-through adds both required window styles without changing topmost");
            Assert(LayeredSurface.WindowStyle(style, false, false) == 8, "Restoring interaction clears click-through and optional layering");
            var registrar = new FakeRegistrar();
            using (var recovery = new RecoveryHotkey(new IntPtr(1), registrar))
            {
                Assert(recovery.TrySet("Ctrl+Alt+F1", out error) && recovery.Registered, "Recovery shortcut registration succeeds");
                int first = 0x4A11;
                int events = registrar.Events.Count;
                Assert(recovery.TrySet("Ctrl+Alt+F1", out error) && registrar.Events.Count == events, "Unchanged shortcut does not re-register");
                registrar.FailNext = true;
                Assert(!recovery.TrySet("Ctrl+Alt+F2", out error) && recovery.Owns(first) && registrar.Active.Count == 1, "Conflicting replacement preserves the working recovery shortcut");
                Assert(recovery.TrySet("Ctrl+Alt+F3", out error) && recovery.Owns(0x4A12) && registrar.Active.Count == 1, "Successful replacement releases the old shortcut");
                Assert(registrar.Events[registrar.Events.Count - 2] == "register:18962" && registrar.Events[registrar.Events.Count - 1] == "unregister:18961", "Replacement is registered before old shortcut release");
                Assert(!recovery.Owns(123), "Unrelated global hotkey messages are ignored");
            }

            Assert(registrar.Active.Count == 0, "Exit unregisters the recovery shortcut");
            var prefs = new Preferences
            {
                ClickThrough = true
            };
            prefs.Hotkeys.GlobalSettings = "Ctrl+Shift+F8";
            var copy = prefs.Clone();
            copy.Hotkeys.Quit = "Ctrl+Shift+Q";
            Assert(prefs.Hotkeys.Quit == "Ctrl+Q", "Cloning preferences does not share mutable hotkey bindings");
            prefs.CopyFrom(Presets.BuiltIn("Classic meters"), true);
            Assert(prefs.ClickThrough && prefs.Hotkeys.GlobalSettings == "Ctrl+Shift+F8" && prefs.View == "Bars", "Appearance presets preserve interaction and shortcut preferences");
            string path = Path.Combine(folder, "interaction-roundtrip.xml");
            prefs.Write(path);
            var loaded = Preferences.Read(path);
            Assert(loaded.ClickThrough && loaded.Hotkeys.GlobalSettings == "Ctrl+Shift+F8", "Interaction and hotkeys persist across settings reload");
            var old = Preferences.Read(Path.Combine(folder, "v1-migration.xml"));
            Assert(!old.ClickThrough && old.Hotkeys.GlobalSettings == "Ctrl+Alt+F1", "Older settings gain safe interaction defaults");
        }

        public static int Run(string path)
        {
            checks.Clear();
            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);
            try
            {
                var samples = new float[8192];
                for (int i = 0; i < samples.Length; i++)
                    samples[i] = (float)(.5 * Math.Sin(2 * Math.PI * 1500 * i / 48000));
                var fft = new Spectrum();
                fft.Compute(samples, 4096);
                int peak = 1;
                for (int i = 2; i < fft.Magnitudes.Length; i++)
                    if (fft.Magnitudes[i] > fft.Magnitudes[peak])
                        peak = i;
                Assert(peak == 128, "FFT tone frequency");
                Assert(Math.Abs(fft.Magnitudes[peak] - .5) < .002, "Hann window amplitude normalization");
                Assert(fft.Band(1400, 1600, 48000, 4096) > .49 && fft.Band(100, 200, 48000, 4096) < .0001, "Frequency band isolation");
                Array.Clear(samples, 0, samples.Length);
                fft.Compute(samples, 8192);
                Assert(fft.Band(35, 20000, 48000, 8192) == 0, "Silent FFT");
                float[] output = new float[2];
                Loopback.Decode(new byte[] { 0, 64, 0, 64, 0, 128, 0, 128 }, output, 2, 2, 4, 16, false);
                Assert(output[0] == .5f && output[1] == -1, "16-bit stereo PCM");
                Loopback.Decode(new byte[] { 255, 255, 127, 0, 0, 128 }, output, 2, 1, 3, 24, false);
                Assert(output[0] > .999f && output[1] == -1, "24-bit signed PCM");
                Loopback.Decode(new byte[] { 0, 255 }, output, 2, 1, 1, 8, false);
                Assert(output[0] == -1 && output[1] > .99, "8-bit unsigned PCM");
                Loopback.Decode(new byte[] { 0, 0, 0, 128, 255, 255, 255, 127 }, output, 2, 1, 4, 32, false);
                Assert(output[0] == -1 && output[1] > .999, "32-bit signed PCM");
                byte[] floats = new byte[8];
                Array.Copy(BitConverter.GetBytes(.25f), 0, floats, 0, 4);
                Array.Copy(BitConverter.GetBytes(float.NaN), 0, floats, 4, 4);
                Loopback.Decode(floats, output, 2, 1, 4, 32, true);
                Assert(output[0] == .25f && output[1] == 0, "Float decode and NaN sanitization");
                Loopback.Decode(BitConverter.GetBytes(-.75), output, 1, 1, 8, 64, true);
                Assert(output[0] == -.75f, "64-bit float decode");
                var p = new Preferences
                {
                    Gain = 999,
                    FftSize = 3,
                    ScopeColor = "bad",
                    View = "bad",
                    Width = -1,
                    MinFrequency = double.NaN,
                    MaxFrequency = double.PositiveInfinity,
                    MidThreshold = 95,
                    HighThreshold = 5
                };
                p.Validate();
                Assert(p.Gain == 36 && p.FftSize == 4096 && p.ScopeColor == "55FFAA" && p.View == "Both" && p.Width == 400, "Invalid and old preferences recover safely");
                Assert(p.MinFrequency == 35 && p.MaxFrequency == 20000 && p.HighThreshold > p.MidThreshold, "Nonfinite ranges and crossed thresholds are repaired");
                Color c;
                Assert(Preferences.TryColor("#12abEF", out c) && c.R == 18 && c.G == 171 && c.B == 239, "Hex color parsing");
                p = new Preferences();
                Assert(Math.Abs(p.ThresholdDb(65) + 25.2) < 1e-8 && Math.Abs(p.ThresholdPercent(p.ThresholdDb(85)) - 85) < 1e-8, "Percent and dBFS thresholds share one canonical scale");
                Assert(MeterMath.LevelColor(p, .64) == Preferences.ColorOf(p.LowColor) && MeterMath.LevelColor(p, .65) == Preferences.ColorOf(p.MidColor) && MeterMath.LevelColor(p, .85) == Preferences.ColorOf(p.HighColor), "Level color boundaries are exact");
                Assert(Math.Abs(MeterMath.FrequencyAt(p, 0, 48000) - 35) < .001 && Math.Abs(MeterMath.FrequencyAt(p, 1, 48000) - 20000) < .001, "Log frequency endpoints match labels");
                p.FrequencyScale = "Linear";
                p.MinFrequency = 500;
                p.MaxFrequency = 5000;
                Assert(MeterMath.FrequencyAt(p, .5, 48000) == 2750, "Linear frequency spacing");
                p.MaxFrequency = 96000;
                Assert(MeterMath.FrequencyAt(p, 1, 32000) == 15680, "Frequency range follows device Nyquist limit");
                Assert(MeterMath.FrequencyLabel(1500) == "1.5 kHz" && MeterMath.FrequencyLabel(55) == "55 Hz", "Adaptive frequency units");
                p = new Preferences
                {
                    GateEnabled = true,
                    GateDb = -40,
                    Gain = 36
                };
                Assert(MeterMath.Level(.001, p) == 0, "Hard cutoff applies before sensitivity gain");
                p.GateEnabled = false;
                double ungated = MeterMath.Level(.007, p);
                p.GateEnabled = true;
                p.GateSoftnessDb = 12;
                double soft = MeterMath.Level(.007, p);
                Assert(soft > 0 && soft < ungated, "Soft cutoff attenuates the edge");
                p = new Preferences
                {
                    LinkSensitivity = false,
                    ScopeGain = -10,
                    BarGain = 20
                };
                Assert(p.EffectiveScopeGain == -10 && p.EffectiveBarGain == 20, "Independent sensitivity");
                p.LinkSensitivity = true;
                Assert(p.EffectiveScopeGain == 6 && p.EffectiveBarGain == 6, "Linked sensitivity");
                double thirty = 1, sixty = 1;
                for (int i = 0; i < 30; i++)
                    thirty = MeterMath.Smooth(thirty, 0, p, 1.0 / 30);
                for (int i = 0; i < 60; i++)
                    sixty = MeterMath.Smooth(sixty, 0, p, 1.0 / 60);
                Assert(Math.Abs(thirty - sixty) < 1e-10, "Release timing is refresh-rate independent");
                var small = SceneLayout.Make(new Size(400, 240), p, 1);
                var large = SceneLayout.Make(new Size(3840, 2160), p, 1);
                Assert(small.Content.Left == 8 && large.Content.Left == 8 && large.Content.Right == 3832, "Margins remain eight pixels at different window sizes");
                var scaled = SceneLayout.Make(new Size(1920, 1080), p, 2);
                Assert(scaled.Content.Left == 16, "Fixed margins respect Windows display scaling");
                p.ScopeColor = "112233";
                p.Gain = 20;
                p.Reset("ScopeColor");
                Assert(p.ScopeColor == "55FFAA" && p.Gain == 20, "Per-control reset preserves unrelated settings");
                p.Reset(Preferences.ResponseFields);
                Assert(p.Gain == 6, "Section reset");
                p.Width = 1234;
                var preset = Presets.BuiltIn("Continuous gradient");
                p.CopyFrom(preset, true);
                Assert(p.Width == 1234 && p.ScopeStyle == "Gradient" && p.GradientCoverage == "Both views", "Preset application preserves window geometry");
                var clone = p.Clone();
                clone.LowColor = "0000FF";
                Assert(p.LowColor != "0000FF", "Presets are independent snapshots");
                string xml = Path.Combine(folder, "v2-preset-roundtrip.xml");
                p.Write(xml);
                var loaded = Preferences.Read(xml);
                Assert(loaded.Width == 1234 && loaded.GradientCoverage == "Both views", "Preset export/import round trip");
                string old = Path.Combine(folder, "v1-migration.xml");
                File.WriteAllText(old, "<Preferences><Gain>12</Gain><View>Bars</View><LowColor>123456</LowColor></Preferences>");
                loaded = Preferences.Read(old);
                Assert(loaded.Gain == 12 && loaded.View == "Bars" && loaded.LowColor == "123456" && loaded.Padding == 8 && loaded.BackgroundOpacity == 100, "Version 1 settings retain values and gain new defaults");
                bool rejected = false;
                try
                {
                    Presets.PathFor("../outside");
                }
                catch (ArgumentException)
                {
                    rejected = true;
                }

                Assert(rejected, "Preset names cannot escape their folder");
                string unsafeXml = Path.Combine(folder, "invalid-preset.xml");
                File.WriteAllText(unsafeXml, "<!DOCTYPE Preferences [<!ENTITY x SYSTEM 'file:///test'>]><Preferences><View>&x;</View></Preferences>");
                rejected = false;
                try
                {
                    Preferences.Read(unsafeXml);
                }
                catch
                {
                    rejected = true;
                }

                Assert(rejected, "Preset XML rejects external entities");
                var signal = Signal();
                p = new Preferences
                {
                    Gain = 18
                };
                using (var bitmap = Render(p, signal, 800, 480))
                {
                    Assert(Count(bitmap, Preferences.ColorOf(p.LowColor)) > 100 && Count(bitmap, Preferences.ColorOf(p.MidColor)) > 20 && Count(bitmap, Preferences.ColorOf(p.HighColor)) > 10, "Green, yellow, and red segmented bars render");
                    bitmap.Save(Path.Combine(folder, "v2-classic.png"));
                }

                p.Segmented = false;
                using (var bitmap = Render(p, signal, 640, 360))
                    Assert(Count(bitmap, Preferences.ColorOf(p.HighColor)) > 10, "Solid bars retain level color zones");
                p = new Preferences
                {
                    BackgroundOpacity = 25
                };
                using (var bitmap = Render(p, signal, 640, 360))
                {
                    Assert(Math.Abs(bitmap.GetPixel(0, 0).A - 64) <= 1, "Background opacity is independent alpha");
                    int max = 0;
                    for (int x = 8; x < 632; x++)
                        for (int y = 8; y < 140; y++)
                            max = Math.Max(max, bitmap.GetPixel(x, y).A);
                    Assert(max > 210, "Scope remains bright over translucent background");
                    bitmap.Save(Path.Combine(folder, "v2-translucent.png"));
                }

                p.BackgroundOpacity = 0;
                using (var bitmap = Render(p, signal, 640, 360))
                    Assert(bitmap.GetPixel(0, 0).A == 1, "Clear retains a clickable alpha-one surface");
                p = new Preferences
                {
                    BarStyle = "Gradient",
                    ScopeStyle = "Gradient",
                    GradientCoverage = "Both views",
                    GradientDirection = "Horizontal",
                    Gain = 18
                };
                ulong forward, reverse;
                using (var bitmap = Render(p, signal, 800, 480))
                {
                    forward = Hash(bitmap);
                    bitmap.Save(Path.Combine(folder, "v2-continuous-gradient.png"));
                }

                p.GradientReverse = true;
                using (var bitmap = Render(p, signal, 800, 480))
                    reverse = Hash(bitmap);
                Assert(forward != reverse, "Reverse gradient changes the actual render");
                foreach (string direction in new[]
                {
                    "Vertical",
                    "Horizontal"
                }

                )
                    foreach (string coverage in new[]
                    {
                        "Each bar",
                        "Bars view",
                        "Both views"
                    }

                    )
                    {
                        p.GradientDirection = direction;
                        p.GradientCoverage = coverage;
                        using (var bitmap = Render(p, signal, 400, 240))
                            Assert(Hash(bitmap) != 0, "Gradient render: " + direction + ", " + coverage);
                    }

                p = new Preferences
                {
                    Gain = 18,
                    BarsLabels = true,
                    ScopeTimeLabels = true,
                    ScopeAmplitudeLabels = true,
                    BarsGrid = true,
                    ScopeGrid = true,
                    MinFrequency = 100,
                    MaxFrequency = 8000
                };
                using (var bitmap = Render(p, signal, 800, 480))
                {
                    bitmap.Save(Path.Combine(folder, "v2-labels.png"));
                    Assert(Count(bitmap, Preferences.ColorOf(p.LowColor)) > 20, "Labels, grids, and changed frequency range render together");
                }

                foreach (string view in new[]
                {
                    "Oscilloscope",
                    "Bars",
                    "Both"
                }

                )
                {
                    p.View = view;
                    p.LabelSize = 24;
                    using (var bitmap = Render(p, signal, 400, 240))
                        Assert(Hash(bitmap) != 0, "Small window with large labels: " + view);
                }

                p = new Preferences
                {
                    GateEnabled = true,
                    GateDb = -1,
                    IdleBars = false,
                    View = "Bars",
                    Peaks = false
                };
                using (var bitmap = Render(p, signal, 400, 240))
                    Assert(Count(bitmap, Color.Black) == 400 * 240, "Cutoff hides all below-threshold bars without phantom peaks");
                p = new Preferences
                {
                    ScopeStyle = "Gradient",
                    BarStyle = "Gradient",
                    GradientDirection = "Horizontal",
                    GradientCoverage = "Both views"
                };
                using (var bitmap = new Bitmap(101, 101))
                using (var g = Graphics.FromImage(bitmap))
                using (var brush = SceneRenderer.Gradient(new RectangleF(0, 0, 100, 100), p, false, 255))
                {
                    g.FillRectangle(brush, 0, 0, 101, 101);
                    Color left = bitmap.GetPixel(1, 50), right = bitmap.GetPixel(98, 50);
                    Assert(left.G > left.R && right.R > right.G, "Shared gradient has the expected endpoint palette");
                }

                using (var ring = new Loopback(false))
                {
                    var append = typeof(Loopback).GetMethod("Append", BindingFlags.Instance | BindingFlags.NonPublic);
                    var source = new float[70000];
                    for (int i = 0; i < source.Length; i++)
                        source[i] = i + 1;
                    append.Invoke(ring, new object[] { source, source.Length });
                    var copy = new float[128];
                    for (int i = 0; i < copy.Length; i++)
                        copy[i] = -99;
                    bool energy;
                    long version = ring.Snapshot(copy, 64, out energy);
                    Assert(energy && ring.HasRecentSignal(64) && copy[127] == 70000 && copy[64] == 69937 && copy[0] == -99, "Bulk ring copy preserves newest requested samples after wraparound");
                    append.Invoke(ring, new object[] { new float[128], 128 });
                    long next = ring.Snapshot(copy, 64, out energy);
                    Assert(!energy && !ring.HasRecentSignal(64) && copy[127] == 0 && copy[0] == -99 && next != version, "Silence detection and sample revisions follow capture data");
                    typeof(Loopback).GetField("lastPacket", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ring, 0L);
                    next = ring.Snapshot(copy, 64, out energy);
                    Assert(!energy && next < 0, "Stale capture yields silence and a distinct sample revision");
                }

                using (var cached = new SceneRenderer())
                using (var bitmap = new Bitmap(400, 240))
                using (var g = Graphics.FromImage(bitmap))
                {
                    p = new Preferences
                    {
                        View = "Bars",
                        Peaks = false
                    };
                    cached.Draw(g, bitmap.Size, p, signal, 48000, .1, 1);
                    ulong before = Hash(bitmap);
                    p.LowColor = "AA00FF";
                    p.MidColor = "AA00FF";
                    p.HighColor = "AA00FF";
                    cached.InvalidateResources();
                    cached.Draw(g, bitmap.Size, p, signal, 48000, .1, 1);
                    Assert(before != Hash(bitmap) && Count(bitmap, Preferences.ColorOf(p.LowColor)) > 20, "Cached resources update when appearance changes");
                    var largeSignal = Signal();
                    cached.SampleVersion = 9;
                    cached.Draw(g, bitmap.Size, p, largeSignal, 48000, .1, 1);
                    var silent = new float[65536];
                    cached.SampleVersion = 10;
                    for (int i = 0; i < 100; i++)
                        cached.Draw(g, bitmap.Size, p, silent, 48000, .1, 1);
                    Assert(Count(bitmap, Preferences.ColorOf(p.LowColor)) == 0, "Cached FFT updates on a new silent sample revision");
                }

                p = new Preferences();
                Assert(!Visualizer.NeedsFrame(true, false, 48000, 48000, 10, p), "Settled silence stops the redraw loop");
                Assert(Visualizer.NeedsFrame(true, true, 48000, 48000, 10, p), "New audio immediately wakes the redraw loop");
                Assert(Visualizer.NeedsFrame(false, false, 48000, 48000, 10, p) && Visualizer.NeedsFrame(true, false, 48000, 44100, 10, p), "Resizes, settings changes, and device rates force a fresh frame");
                checks.Add("INFO  Native array drawing enabled: " + NativeDrawing.Enabled);
                bool nativeEnabled = NativeDrawing.Enabled;
                try
                {
                    p = new Preferences
                    {
                        BarStyle = "Gradient",
                        ScopeStyle = "Gradient",
                        GradientCoverage = "Both views",
                        BackgroundOpacity = 25,
                        BarsLabels = true,
                        ScopeTimeLabels = true,
                        ScopeGrid = true
                    };
                    using (var native = Render(p, signal, 800, 480))
                    {
                        NativeDrawing.Enabled = false;
                        using (var fallback = Render(p, signal, 800, 480))
                            Assert(Hash(native) == Hash(fallback), "Native array drawing matches the public API fallback");
                    }
                }
                finally
                {
                    NativeDrawing.Enabled = nativeEnabled;
                }

                using (var renderer = new SceneRenderer())
                using (var bitmap = new Bitmap(400, 240))
                using (var g = Graphics.FromImage(bitmap))
                {
                    p = new Preferences
                    {
                        View = "Bars"
                    };
                    renderer.SampleVersion = 1;
                    renderer.Draw(g, bitmap.Size, p, signal, 48000, .1, 1);
                    renderer.ClearAnimation();
                    renderer.SampleVersion = 2;
                    renderer.Draw(g, bitmap.Size, p, new float[65536], 48000, .001, 1);
                    Assert(Count(bitmap, Preferences.ColorOf(p.LowColor)) == 0 && Count(bitmap, Preferences.ColorOf(p.MidColor)) == 0 && Count(bitmap, Preferences.ColorOf(p.HighColor)) == 0, "The final idle frame clears old bars and peaks");
                }

                InteractionChecks(folder);
                using (var owner = new Visualizer(new Preferences()))
                using (var settings = new SettingsForm(owner, new Preferences()))
                {
                    Assert(owner.Text == AppInfo.FullName && settings.Text.Contains(AppInfo.FullName), "Full application name is used for window titles");
                    settings.PerformLayout();
                    Control caption = Find(settings, "Background opacity (%)");
                    Assert(caption != null && caption.Bottom <= caption.Parent.ClientSize.Height, "Grouped slider captions stay within their row");
                    caption = Find(settings, "Window size");
                    Assert(caption != null && caption.Bottom <= caption.Parent.ClientSize.Height, "Window size caption stays visible");
                    var select = typeof(SettingsForm).GetMethod("BuildPage", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int section = 0; section < 7; section++)
                    {
                        select.Invoke(settings, new object[] { section });
                        settings.PerformLayout();
                        Assert(settings.Controls.Count > 0, "Settings section layout: " + section);
                    }

                    select.Invoke(settings, new object[] { 4 });
                    settings.PerformLayout();
                    caption = Find(settings, "Bars / low");
                    Assert(caption != null && caption.Bottom <= caption.Parent.ClientSize.Height, "Color captions stay within their row");
                    using (var bitmap = new Bitmap(settings.Width, settings.Height))
                    {
                        settings.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                        bitmap.Save(Path.Combine(folder, "v2-settings.png"));
                    }
                }

                File.WriteAllLines(path, checks);
                return 0;
            }
            catch (Exception ex)
            {
                checks.Add("FAIL  " + ex);
                File.WriteAllLines(path, checks);
                return 1;
            }
        }

        public static int Probe(string path)
        {
            using (var audio = new Loopback())
            {
                System.Threading.Thread.Sleep(2500);
                var samples = new float[8192];
                audio.Snapshot(samples);
                double sum = 0;
                foreach (float sample in samples)
                    sum += sample * sample;
                File.WriteAllText(path, audio.Status + "\r\nFrames captured: " + audio.Frames + "\r\nRecent RMS: " + Math.Sqrt(sum / samples.Length) + "\r\n");
                return audio.Status.StartsWith("Listening") ? 0 : 1;
            }
        }
    }
}
