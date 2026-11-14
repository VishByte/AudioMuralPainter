using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace Amp
{
    public sealed class Preferences
    {
        public string View = "Both";
        public int Gain = 6, ScopeGain = 6, BarGain = 6;
        public bool LinkSensitivity = true;
        public int Smoothing = 55; // Retained so version 1 files remain readable.
        public int AttackMs = 12, ReleaseMs = 410, PeakHoldMs = 320, PeakFallMs = 2800;
        public int Bars = 48, FftSize = 4096, WavePoints = 1024, TimeMs = 40, Fps = 60;
        public int Width = 1000, Height = 600, Left = -32000, Top = -32000;
        public bool Peaks = true, OnTop = false;
        public bool ClickThrough = false;
        public HotkeyBindings Hotkeys = new HotkeyBindings();
        public int BackgroundOpacity = 100, Padding = 8, ViewGap = 12, ScopeShare = 40;
        public double BarGap = 3, SegmentHeight = 6, SegmentGap = 2, WaveThickness = 1.4;
        public int WaveGlow = 40, IdleOpacity = 12;
        public bool Segmented = true, IdleBars = true;
        public double MinFrequency = 35, MaxFrequency = 20000;
        public string FrequencyScale = "Logarithmic";
        public int MeterFloorDb = -72, GateDb = -60, GateSoftnessDb = 0;
        public bool GateEnabled = false;
        public double MidThreshold = 65, HighThreshold = 85;
        public string ThresholdUnit = "Percent";
        public string BarStyle = "Level colors", ScopeStyle = "Solid";
        public string GradientDirection = "Vertical", GradientCoverage = "Each bar";
        public bool GradientReverse = false;
        public string ScopeColor = "55FFAA", LowColor = "24E778", MidColor = "FFD34E", HighColor = "FF505C";
        public string ScopeGradientMid = "42CFFF", ScopeGradientEnd = "AA55FF";
        public bool BarsLabels = false, ScopeTimeLabels = false, ScopeAmplitudeLabels = false;
        public bool BarsGrid = false, ScopeGrid = false;
        public int LabelSize = 9, LabelOpacity = 75, LabelDensity = 6, GridOpacity = 14;
        public string LabelColor = "B7C8C0";
        public static readonly string[] DisplayFields =
        {
            "View",
            "BackgroundOpacity",
            "Fps",
            "Padding",
            "ViewGap",
            "ScopeShare",
            "OnTop",
            "ClickThrough"
        };
        public static readonly string[] BarsFields =
        {
            "Bars",
            "FftSize",
            "MinFrequency",
            "MaxFrequency",
            "FrequencyScale",
            "MeterFloorDb",
            "GateEnabled",
            "GateDb",
            "GateSoftnessDb",
            "Segmented",
            "BarGap",
            "SegmentHeight",
            "SegmentGap",
            "IdleBars",
            "IdleOpacity",
            "Peaks"
        };
        public static readonly string[] ScopeFields =
        {
            "WavePoints",
            "TimeMs",
            "WaveThickness",
            "WaveGlow"
        };
        public static readonly string[] ResponseFields =
        {
            "LinkSensitivity",
            "Gain",
            "ScopeGain",
            "BarGain",
            "AttackMs",
            "ReleaseMs",
            "PeakHoldMs",
            "PeakFallMs"
        };
        public static readonly string[] ColorFields =
        {
            "ScopeColor",
            "LowColor",
            "MidColor",
            "HighColor",
            "ScopeGradientMid",
            "ScopeGradientEnd",
            "MidThreshold",
            "HighThreshold",
            "ThresholdUnit",
            "BarStyle",
            "ScopeStyle",
            "GradientDirection",
            "GradientReverse",
            "GradientCoverage"
        };
        public static readonly string[] LabelFields =
        {
            "BarsLabels",
            "ScopeTimeLabels",
            "ScopeAmplitudeLabels",
            "BarsGrid",
            "ScopeGrid",
            "LabelSize",
            "LabelOpacity",
            "LabelDensity",
            "GridOpacity",
            "LabelColor"
        };
        static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        static double Clamp(double value, double min, double max, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
        }

        static string Choice(string value, string fallback, params string[] options)
        {
            return Array.IndexOf(options, value) >= 0 ? value : fallback;
        }

        public void Validate()
        {
            if (Hotkeys == null)
                Hotkeys = new HotkeyBindings();
            Hotkeys.Normalize();
            View = Choice(View, "Both", "Both", "Oscilloscope", "Bars");
            Gain = Clamp(Gain, -18, 36);
            ScopeGain = Clamp(ScopeGain, -18, 36);
            BarGain = Clamp(BarGain, -18, 36);
            AttackMs = Clamp(AttackMs, 1, 500);
            ReleaseMs = Clamp(ReleaseMs, 10, 3000);
            PeakHoldMs = Clamp(PeakHoldMs, 0, 3000);
            PeakFallMs = Clamp(PeakFallMs, 100, 10000);
            Bars = Clamp(Bars, 4, 256);
            WavePoints = Clamp(WavePoints, 128, 8192);
            TimeMs = Clamp(TimeMs, 1, 200);
            if (Array.IndexOf(new[] { 1024, 2048, 4096, 8192, 16384, 32768 }, FftSize) < 0)
                FftSize = 4096;
            if (Array.IndexOf(new[] { 30, 60, 120 }, Fps) < 0)
                Fps = 60;
            Width = Clamp(Width, 400, 7680);
            Height = Clamp(Height, 240, 4320);
            BackgroundOpacity = Clamp(BackgroundOpacity, 0, 100);
            Padding = Clamp(Padding, 0, 64);
            ViewGap = Clamp(ViewGap, 0, 64);
            ScopeShare = Clamp(ScopeShare, 15, 85);
            BarGap = Clamp(BarGap, 0, 32, 3);
            SegmentHeight = Clamp(SegmentHeight, 1, 32, 6);
            SegmentGap = Clamp(SegmentGap, 0, 16, 2);
            WaveThickness = Clamp(WaveThickness, .5, 8, 1.4);
            WaveGlow = Clamp(WaveGlow, 0, 100);
            IdleOpacity = Clamp(IdleOpacity, 0, 100);
            MinFrequency = Clamp(MinFrequency, 1, 95999, 35);
            MaxFrequency = Clamp(MaxFrequency, 2, 96000, 20000);
            if (MaxFrequency <= MinFrequency)
                MaxFrequency = Math.Min(96000, MinFrequency + 1);
            FrequencyScale = Choice(FrequencyScale, "Logarithmic", "Logarithmic", "Linear");
            MeterFloorDb = Clamp(MeterFloorDb, -120, -12);
            GateDb = Clamp(GateDb, -120, 0);
            GateSoftnessDb = Clamp(GateSoftnessDb, 0, 24);
            MidThreshold = Clamp(MidThreshold, 1, 98, 65);
            HighThreshold = Clamp(HighThreshold, 2, 99, 85);
            if (HighThreshold <= MidThreshold)
                HighThreshold = Math.Min(99, MidThreshold + 1);
            ThresholdUnit = Choice(ThresholdUnit, "Percent", "Percent", "dBFS");
            BarStyle = Choice(BarStyle, "Level colors", "Level colors", "Gradient");
            ScopeStyle = Choice(ScopeStyle, "Solid", "Solid", "Gradient");
            GradientDirection = Choice(GradientDirection, "Vertical", "Vertical", "Horizontal");
            GradientCoverage = Choice(GradientCoverage, "Each bar", "Each bar", "Bars view", "Both views");
            ScopeColor = NormalizeColor(ScopeColor, "55FFAA");
            LowColor = NormalizeColor(LowColor, "24E778");
            MidColor = NormalizeColor(MidColor, "FFD34E");
            HighColor = NormalizeColor(HighColor, "FF505C");
            ScopeGradientMid = NormalizeColor(ScopeGradientMid, "42CFFF");
            ScopeGradientEnd = NormalizeColor(ScopeGradientEnd, "AA55FF");
            LabelColor = NormalizeColor(LabelColor, "B7C8C0");
            LabelSize = Clamp(LabelSize, 6, 24);
            LabelOpacity = Clamp(LabelOpacity, 0, 100);
            LabelDensity = Clamp(LabelDensity, 2, 16);
            GridOpacity = Clamp(GridOpacity, 0, 100);
        }

        static string NormalizeColor(string text, string fallback)
        {
            Color c;
            return TryColor(text, out c) ? Hex(c) : fallback;
        }

        public static string Hex(Color color)
        {
            return color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        public static bool TryColor(string text, out Color color)
        {
            color = Color.White;
            if (text == null)
                return false;
            text = text.Trim().TrimStart('#');
            int hex;
            if (text.Length != 6 || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out hex))
                return false;
            color = Color.FromArgb((hex >> 16) & 255, (hex >> 8) & 255, hex & 255);
            return true;
        }

        public static Color ColorOf(string text)
        {
            Color c;
            TryColor(text, out c);
            return c;
        }

        public double ThresholdDb(double percentage)
        {
            return MeterFloorDb * (1 - percentage / 100);
        }

        public double ThresholdPercent(double db)
        {
            return 100 * (db - MeterFloorDb) / -MeterFloorDb;
        }

        public int EffectiveScopeGain
        {
            get
            {
                return LinkSensitivity ? Gain : ScopeGain;
            }
        }

        public int EffectiveBarGain
        {
            get
            {
                return LinkSensitivity ? Gain : BarGain;
            }
        }

        public void Reset(params string[] fields)
        {
            var defaults = new Preferences();
            foreach (string name in fields)
            {
                FieldInfo f = typeof(Preferences).GetField(name);
                f.SetValue(this, f.GetValue(defaults));
            }

            Validate();
        }

        public Preferences Clone()
        {
            var copy = new Preferences();
            copy.CopyFrom(this, false);
            return copy;
        }

        public void CopyFrom(Preferences source, bool preserveWindow)
        {
            foreach (var field in typeof(Preferences).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (preserveWindow && (field.Name == "Width" || field.Name == "Height" || field.Name == "Left" || field.Name == "Top" || field.Name == "ClickThrough" || field.Name == "Hotkeys"))
                    continue;
                field.SetValue(this, field.Name == "Hotkeys" ? (source.Hotkeys ?? new HotkeyBindings()).Clone() : field.GetValue(source));
            }

            Validate();
        }

        public static string FilePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Amp.settings.xml");
            }
        }

        public static Preferences Read(string path)
        {
            if (new FileInfo(path).Length > 1048576)
                throw new InvalidDataException("This preset is too large.");
            var options = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1048576
            };
            using (var reader = XmlReader.Create(path, options))
            {
                var p = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(reader);
                p.Validate();
                return p;
            }
        }

        public static Preferences Load()
        {
            try
            {
                return Read(FilePath);
            }
            catch
            {
                return new Preferences();
            }
        }

        public void Write(string path)
        {
            Validate();
            string temp = path + ".tmp";
            try
            {
                using (var stream = File.Create(temp))
                    new XmlSerializer(typeof(Preferences)).Serialize(stream, this);
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
        }

        public string Save()
        {
            try
            {
                Write(FilePath);
                return null;
            }
            catch (Exception ex)
            {
                return "Could not save preferences: " + ex.Message;
            }
        }
    }

    internal static class Presets
    {
        public static string Folder
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Presets");
            }
        }

        public static string PathFor(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 48 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == ".." || name.EndsWith("."))
                throw new ArgumentException("Choose a name of 1–48 characters without file path characters.");
            return Path.Combine(Folder, name + ".xml");
        }

        public static string[] Names()
        {
            if (!Directory.Exists(Folder))
                return new string[0];
            return Array.ConvertAll(Directory.GetFiles(Folder, "*.xml"), Path.GetFileNameWithoutExtension);
        }

        public static void Save(string name, Preferences p)
        {
            string path = PathFor(name);
            Directory.CreateDirectory(Folder);
            p.Write(path);
        }

        public static Preferences BuiltIn(string name)
        {
            var p = new Preferences();
            if (name == "Minimal scope")
            {
                p.View = "Oscilloscope";
                p.WaveGlow = 20;
            }

            if (name == "Classic meters")
            {
                p.View = "Bars";
                p.Bars = 32;
                p.BarsLabels = true;
            }

            if (name == "Continuous gradient")
            {
                p.BarStyle = "Gradient";
                p.ScopeStyle = "Gradient";
                p.GradientCoverage = "Both views";
                p.GradientDirection = "Horizontal";
            }

            if (name == "Glass")
            {
                p.BackgroundOpacity = 25;
                p.BarStyle = "Gradient";
                p.ScopeStyle = "Gradient";
            }

            return p;
        }
    }
}
