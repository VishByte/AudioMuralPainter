using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Amp
{
    internal static class SecurityTests
    {
        static bool Rejects(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidDataException)
            {
                return true;
            }
            catch (System.Xml.XmlException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        public static void Run(string folder, Action<bool, string> check)
        {
            string path = Path.Combine(folder, "security-input.xml");
            string[] payloads =
            {
                "",
                "<WrongRoot/>",
                "<Preferences>",
                "<Preferences xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/>",
                "<!DOCTYPE Preferences [<!ENTITY x SYSTEM 'file:///C:/nonexistent-amp-canary'>]><Preferences><View>&x;</View></Preferences>",
                "<!DOCTYPE Preferences SYSTEM 'http://127.0.0.1:1/amp-canary'><Preferences/>",
                "<!DOCTYPE Preferences [<!ENTITY a 'payload'><!ENTITY b '&a;&a;&a;&a;'>]><Preferences><View>&b;</View></Preferences>",
                "<Preferences><Width>99999999999999999999999</Width></Preferences>",
                "<Preferences><Hotkeys><Settings>" + new string ('x', 1048576) + "</Settings></Hotkeys></Preferences>",
                "<Preferences>" + Repeat("<unknown>", 128) + Repeat("</unknown>", 128) + "</Preferences>"
            };
            for (int i = 0; i < payloads.Length; i++)
            {
                File.WriteAllText(path, payloads[i]);
                check(Rejects(delegate
                {
                    Preferences.Read(path);
                }), "Adversarial XML rejected: " + i);
            }

            File.WriteAllText(path, "<Preferences><MinFrequency>NaN</MinFrequency><BarGap>INF</BarGap><HighThreshold>-INF</HighThreshold><Hotkeys xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:nil='true'/></Preferences>");
            var imported = Preferences.Read(path);
            check(imported.MinFrequency == 35 && imported.BarGap == 3 && imported.HighThreshold == 85 && imported.Hotkeys != null, "Nonfinite XML values and null hotkeys normalize safely");
            string[] badNames =
            {
                "../escape",
                "..\\escape",
                "C:\\escape",
                "\\\\server\\share",
                "preset:stream",
                "a/b",
                "a\\b",
                ".",
                "..",
                "name.",
                "CON",
                "con.xml",
                "CON .xml",
                "NUL",
                "AUX",
                "PRN",
                "COM1",
                "COM¹",
                "LPT9",
                "LPT²",
                "CONIN$",
                "CONOUT$",
                "bad\0name",
                new string ('x', 49)
            };
            foreach (string name in badNames)
                check(Rejects(delegate
                {
                    Presets.PathFor(name);
                }), "Unsafe preset filename rejected: " + Array.IndexOf(badNames, name));
            check(Path.GetDirectoryName(Presets.PathFor("A normal preset")) == Presets.Folder, "Valid named preset stays in its library");
            var personal = new Preferences
            {
                Left = 12345,
                Top = -23456,
                Width = 1777,
                Height = 999,
                ClickThrough = true,
                LowColor = "123456"
            };
            personal.Hotkeys.GlobalSettings = "Ctrl+Shift+F8";
            personal.WritePreset(path);
            string shared = File.ReadAllText(path);
            foreach (string field in new[]
            {
                "Left",
                "Top",
                "Width",
                "Height",
                "Hotkeys",
                "ClickThrough"
            }

            )
                check(!shared.Contains("<" + field), "Shared preset excludes private field: " + field);
            imported = Preferences.Read(path);
            check(imported.LowColor == "123456" && imported.Left == -32000 && !imported.ClickThrough, "Appearance-only preset round trip uses safe local defaults");
            check(personal.Left == 12345 && personal.Hotkeys.GlobalSettings == "Ctrl+Shift+F8" && personal.ClickThrough, "Export leaves the user's preferences unchanged");
            File.WriteAllText(path + ".tmp", "unrelated-file-canary");
            personal.Write(path);
            check(File.ReadAllText(path + ".tmp") == "unrelated-file-canary", "Atomic save does not overwrite a predictable neighboring file");
            check(Preferences.Read(path).Left == 12345, "Local preferences still retain geometry");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool failed = false;
                try
                {
                    new Preferences().Write(path);
                }
                catch (IOException)
                {
                    failed = true;
                }

                check(failed, "A locked destination rejects replacement without deleting it");
            }

            check(Preferences.Read(path).Left == 12345 && Directory.GetFiles(folder, ".amp-*.tmp").Length == 0, "Failed save preserves existing data and cleans its temporary file");
            Keys key;
            string error;
            check(!Shortcut.TryParse(new string ('A', 10000), out key), "Oversized hotkey input rejected before conversion");
            check(!Shortcut.TryParse("1073741889", out key), "Unknown hotkey modifier bits rejected");
            check(!Shortcut.TryParse("Ctrl+Alt+LButton", out key) && !Shortcut.TryParse("Ctrl+Alt+Packet", out key), "Recovery shortcuts cannot use mouse or synthetic-only key codes");
            check(!new HotkeyBindings { GlobalSettings = "Ctrl+F12" }.Validate(out error), "Windows-reserved global F12 is rejected");
            using (var editor = new TextBox())
            {
                check(SettingsForm.IsTextEditingKey(editor, Keys.Q) && SettingsForm.IsTextEditingKey(editor, Keys.Shift | Keys.Q), "Typing a preset name does not invoke unmodified app shortcuts");
                check(!SettingsForm.IsTextEditingKey(editor, Keys.F1) && !SettingsForm.IsTextEditingKey(editor, Keys.Control | Keys.Q), "Function and modifier shortcuts remain available in editors");
            }

            using (var bitmap = new Bitmap(8, 8))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.White))
            {
                check(Rejects(delegate
                {
                    NativeDrawing.Rectangles(graphics, brush, new RectangleF[1], 2);
                }), "Native rectangle count cannot exceed the pinned array");
                check(Rejects(delegate
                {
                    NativeDrawing.Rectangles(graphics, brush, new RectangleF[1], -1);
                }), "Negative native rectangle counts rejected");
                check(Rejects(delegate
                {
                    NativeDrawing.Lines(graphics, Pens.White, new PointF[1]);
                }), "Native polyline requires at least two points");
            }

            using (var ring = new Loopback(false))
            {
                check(Rejects(delegate
                {
                    ring.Snapshot(new float[0]);
                }), "Empty audio destination rejected safely");
            }

            check(Rejects(delegate
            {
                PcmDecoder.Decode(new byte[4], new float[2], 2, 1, 4, 32, true);
            }), "Truncated PCM buffer rejected before decoding");
            check(Rejects(delegate
            {
                PcmDecoder.Decode(new byte[4], new float[1], 1, 0, 4, 32, true);
            }), "Zero audio channels rejected");
            check(Rejects(delegate
            {
                new Spectrum().Compute(new float[64], 3);
            }), "Non-power-of-two FFT rejected without entering transform loops");
            check(Rejects(delegate
            {
                new Spectrum().Compute(new float[64], 128);
            }), "Oversized FFT rejected before sample indexing");
            check(Rejects(delegate
            {
                new Spectrum().Compute(new float[2], 2);
            }), "Degenerate two-sample Hann window rejected");
            var endpointSignal = new float[1024];
            var endpointSpectrum = new Spectrum();
            for (int i = 0; i < endpointSignal.Length; i++)
                endpointSignal[i] = .5f;
            endpointSpectrum.Compute(endpointSignal, 1024);
            check(Math.Abs(endpointSpectrum.Magnitudes[0] - .5) < 1e-9, "DC FFT amplitude is not doubled");
            for (int i = 0; i < endpointSignal.Length; i++)
                endpointSignal[i] = i % 2 == 0 ? .5f : -.5f;
            endpointSpectrum.Compute(endpointSignal, 1024);
            check(Math.Abs(endpointSpectrum.Magnitudes[512] - .5) < 1e-9, "Nyquist FFT amplitude is not doubled");
            // Deterministic boundary fuzzing keeps failures reproducible and bounded.
            var random = new Random(73129);
            var fuzzSignal = new float[65536];
            for (int i = 0; i < fuzzSignal.Length; i++)
                fuzzSignal[i] = (float)(.75 * Math.Sin(2 * Math.PI * 110 * i / 48000) + .2 * Math.Sin(2 * Math.PI * 1700 * i / 48000));
            for (int i = 0; i < 128; i++)
            {
                var p = new Preferences
                {
                    View = i % 3 == 0 ? "Bars" : i % 3 == 1 ? "Oscilloscope" : "Both",
                    Bars = random.Next(-100, 400),
                    FftSize = new[]
                    {
                        1024,
                        32768,
                        4096,
                        3
                    }[i % 4],
                    WavePoints = random.Next(-10, 10000),
                    Padding = random.Next(-10, 100),
                    LabelSize = random.Next(-10, 40),
                    ScopeShare = random.Next(-100, 200),
                    MinFrequency = i % 2 == 0 ? double.NaN : random.NextDouble() * 100000,
                    MaxFrequency = random.NextDouble() * 100000,
                    MidThreshold = random.NextDouble() * 150,
                    HighThreshold = random.NextDouble() * 150,
                    BarStyle = "Gradient",
                    ScopeStyle = "Gradient",
                    Segmented = i % 2 == 0,
                    ScopeAmplitudeLabels = true,
                    ScopeTimeLabels = true,
                    BarsLabels = true
                };
                p.Validate();
                using (var renderer = new SceneRenderer())
                using (var bitmap = new Bitmap(400, 240))
                using (var graphics = Graphics.FromImage(bitmap))
                    renderer.Draw(graphics, bitmap.Size, p, fuzzSignal, new[] { 8000, 44100, 96000, 384000 }[i % 4], 1.0 / 60, 1 + (i % 3) * .5f);
            }

            check(true, "128 seeded boundary configurations validate and render without crashing");
            check(SceneRenderer.ScopeSampleCount(65536, 8000, 1) == 8, "A one-millisecond scope window stays accurate at 8 kHz");
            check(SceneRenderer.ScopeSampleCount(65536, 384000, 200) == 61694, "High-rate scope timing is limited to available samples for both plot and labels");
            foreach (float level in new[]
            {
                -1f,
                -.5f,
                .5f,
                1f
            }

            )
            {
                using (var wave = ScopeImage(level, false))
                using (var grid = ScopeImage(0, true))
                {
                    int row = 0, brightness = -1;
                    for (int y = 8; y < 232; y++)
                    {
                        int value = wave.GetPixel(40, y).B;
                        if (value > brightness)
                        {
                            brightness = value;
                            row = y;
                        }
                    }

                    bool aligned = false;
                    for (int y = Math.Max(8, row - 1); y <= Math.Min(231, row + 1); y++)
                    {
                        Color pixel = grid.GetPixel(40, y);
                        if (pixel.R > 180 && pixel.G < 50 && pixel.B < 50)
                            aligned = true;
                    }

                    check(brightness > 100 && aligned, "Scope amplitude grid aligns with rendered sample level " + level.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            var search = (System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute)Attribute.GetCustomAttribute(typeof(Preferences).Assembly, typeof(System.Runtime.InteropServices.DefaultDllImportSearchPathsAttribute));
            check(search != null && search.Paths == System.Runtime.InteropServices.DllImportSearchPath.System32, "Native dependencies resolve from Windows System32");
        }

        static string Repeat(string text, int count)
        {
            var result = new StringBuilder();
            for (int i = 0; i < count; i++)
                result.Append(text);
            return result.ToString();
        }

        static Bitmap ScopeImage(float level, bool grid)
        {
            var prefs = new Preferences
            {
                View = "Oscilloscope",
                Gain = 0,
                WaveGlow = 0,
                ScopeColor = grid ? "000000" : "FFFFFF",
                ScopeGrid = grid,
                GridOpacity = 100,
                LabelColor = "FF0000"
            };
            var signal = new float[65536];
            for (int i = 0; i < signal.Length; i++)
                signal[i] = level;
            var bitmap = new Bitmap(400, 240);
            using (var renderer = new SceneRenderer())
            using (var graphics = Graphics.FromImage(bitmap))
                renderer.Draw(graphics, bitmap.Size, prefs, signal, 48000, 1.0 / 60, 1);
            return bitmap;
        }
    }
}
