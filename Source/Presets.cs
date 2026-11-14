using System;
using System.IO;

namespace Amp
{
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
            string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".IndexOf(stem[3]) >= 0))
                throw new ArgumentException("This preset name is reserved by Windows.");
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
            p.WritePreset(path);
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
